from typing import Optional, Tuple, List, Any, Callable, Union, Sequence
from mlagents_envs.base_env import BaseEnv
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.envs.unity_parallel_env import UnityParallelEnv
from pathlib import Path
import multiprocessing as mp
import cloudpickle
import numpy as np
from dataclasses import dataclass
from collections import defaultdict

Observation = List[np.ndarray]
# This wrapper is designed only for parallel pettingZoo API to satisfy a POSG definition
N_TRIES: int = 1000


"""
Some parts of this script adapted from COLTRA-RL library by @RedTachyon
https://github.com/RedTachyon/coltra-rl/blob/master/coltra/envs/subproc_vec_env.py#L77
"""


def parse_agent_name(name: str) -> dict[str, str]:
    """
    Parses an agent's name to distict'em
    returns a dict {
    "name": a name given by unity
    "subname" (env): index of vectorized environment 
    }
    MAPFAgentBehaviour?team=0?agent_id=0&env=3
    """
    parts = name.split("&")
    result = {"name": parts[0]}
    for part in parts[1:]:
        subname, value = part.split("=")
        result[subname] = value

    return result

class CloudpickleWrapper(object):
    def __init__(self, var):
        """
        Uses cloudpickle to serialize contents (otherwise multiprocessing tries to use pickle)

        :param var: (Any) the variable you wish to wrap for pickling with cloudpickle
        """
        self.var = var

    def __getstate__(self):
        return cloudpickle.dumps(self.var)

    def __setstate__(self, obs):
        self.var = cloudpickle.loads(obs)



def _worker(
    remote,
    parent_remote,
    env_fn_wrapper: CloudpickleWrapper,
) -> None:
    parent_remote.close()
    env = env_fn_wrapper.var()
    i_try = 0
    #to prevent creating an infinite loop
    while True and i_try < N_TRIES:
        try:
            i_try+=1
            cmd, data = remote.recv()
            if cmd == "step":
                observation, reward, done, info = env.step(data)
                remote.send((observation, reward, done, info))
            elif cmd == "seed":
                remote.send(env.seed(data))
            elif cmd == "reset":
                observation = env.reset(**data)
                remote.send(observation)
            elif cmd == "render":
                remote.send(env.render("rgb_array"))
            elif cmd == "close":
                env.close()
                remote.close()
                break
            elif cmd == "get_spaces":
                remote.send((env.observation_spaces, env.action_spaces))
            elif cmd == "env_method":
                method = getattr(env, data[0])
                remote.send(method(*data[1], **data[2]))
            elif cmd == "get_attr":
                remote.send(getattr(env, data))
            elif cmd == "set_attr":
                remote.send(setattr(env, data[0], data[1]))
            elif cmd == "observation_space":
                remote.send(env.observation_space(data))
            elif cmd == "action_space":
                remote.send(env.action_space(data))
            elif cmd == "envs":
                remote.send(env)
            else:
                raise NotImplementedError(f"`{cmd}` is not implemented in the worker")
        except EOFError:
            break

class PeekabooEnv(UnityParallelEnv):
    def __init__(self, executable_path: Path, 
                 seed: int = None, 
                 no_grahics: bool = True, 
                 worker_id: int = None,
                 inner_observation_stack: int = 1):
        """
        Notice: Currently communication between Unity and Python takes place over an open socket without authentication. 
        Ensure that the network where training takes place is secure.

        Notice: development build only 
        """
        self._worker_id: int = 0 if worker_id is None else worker_id    
        unity_env = UnityEnvironment(file_name=executable_path, 
                               worker_id=self._worker_id, 
                               seed=seed,
                               no_graphics=no_grahics)
        super().__init__(unity_env, seed)
        self.obs_stack = inner_observation_stack

    #TODO: auxillary stuff to work with python training more smoothly
    @staticmethod
    def pack(dict_: dict[str, Observation]) -> Tuple[Observation, List[str]]:
        keys = list(dict_.keys())
        values = [dict_[key] for key in keys]

        return values, keys

    @staticmethod
    def unpack(arrays: List, keys: List[str]) -> dict[str, Any]:
        value_dict = {key: arrays[i] for i, key in enumerate(keys)}
        return value_dict


def create_env_factory(env_kwargs: dict, worker_id: int):
    """A dummy env factory which returns a function instantianting Peekaboo env"""
    create_peekaboo_env = lambda: PeekabooEnv(worker_id=worker_id, **env_kwargs)
    return create_peekaboo_env

@dataclass
class SubprocVecEnv:

    env_fns: list[Callable[[], PeekabooEnv]]
    start_method: Optional[str] = None

    def __post_init__(self) -> None:    
        self.waiting: bool = False
        self.closed: bool = False

        n_envs = len(self.env_fns)

        if self.start_method is None:
            # Fork is not a thread safe method (see issue #217)
            # but is more user friendly (does not require to wrap the code in
            # a `if __name__ == "__main__":`)
            forkserver_available = "forkserver" in mp.get_all_start_methods()
            self.start_method = "forkserver" if forkserver_available else "spawn"
        ctx = mp.get_context(self.start_method)

        self.remotes, self.work_remotes = zip(
            *[ctx.Pipe(duplex=True) for _ in range(n_envs)]
        )
        self.processes = []
        for work_remote, remote, env_fn in zip(
            self.work_remotes, self.remotes, self.env_fns
        ):
            args = (work_remote, remote, CloudpickleWrapper(env_fn))
            # daemon=True: if the main process crashes, we should not cause things to hang
            process = ctx.Process(
                target=_worker, args=args, daemon=True
            )  # pytype:disable=attribute-error
            process.start()
            self.processes.append(process)
            work_remote.close()

        self.remotes[0].send(("get_spaces", None))
        observation_space, action_space = self.remotes[0].recv()
        self.num_envs = n_envs
        self.observation_space = observation_space
        self.action_space = action_space

    def step_async(self, actions):
        """Send the actons to the environments"""
        for i, remote in enumerate(self.remotes):
            action = {
                "&".join(k.split("&")[:-1]): a
                for k, a in actions.items()
                if int(parse_agent_name(k)["env"]) == i
            }
            remote.send(("step", action))
        
        self.waiting = True

    def step_wait(self):
        results = [remote.recv() for remote in self.remotes]
        self.waiting = False
        obs, rews, dones, infos = zip(*results)
        # infos - tuple of dicts
        return (
            _gather_subproc(obs),
            _gather_subproc(rews),
            _gather_subproc(dones),
            _flatten_info(infos),
        )

    def step(self, actions):
        """
        Step the environments with the given action

        :param actions: ([int] or [float]) the action
        :return: ([int] or [float], [float], [bool], dict) observation, reward, done, information
        """
        self.step_async(actions)
        return self.step_wait()

    def seed(self, seed=None):
        for idx, remote in enumerate(self.remotes):
            remote.send(("seed", seed + idx))
        return [remote.recv() for remote in self.remotes]

    def reset(self, **kwargs) -> dict[str, Observation]:
        for remote in self.remotes:
            remote.send(("reset", kwargs))
        obs = [remote.recv() for remote in self.remotes]
        return _gather_subproc(obs)

    def close(self):
        if self.closed:
            return
        if self.waiting:
            for remote in self.remotes:
                remote.recv()
        for remote in self.remotes:
            remote.send(("close", None))
        for process in self.processes:
            process.join()
        self.closed = True

    def get_images(self) -> Sequence[np.ndarray]:
        for pipe in self.remotes:
            # gather images from subprocesses
            # `mode` will be taken into account later
            pipe.send(("render", "rgb_array"))
        imgs = [pipe.recv() for pipe in self.remotes]
        return imgs

    def render(self, **kwargs) -> np.ndarray:
        pipe = self.remotes[0]
        pipe.send(("render", "rgb_array"))
        img = pipe.recv()
        return img

    def get_attr(self, attr_name, indices=None):
        """Return attribute from vectorized environment (see base class)."""
        target_remotes = self._get_target_remotes(indices)
        for remote in target_remotes:
            remote.send(("get_attr", attr_name))
        return [remote.recv() for remote in target_remotes]

    def set_attr(self, attr_name, value, indices=None):
        """Set attribute inside vectorized environments (see base class)."""
        target_remotes = self._get_target_remotes(indices)
        for remote in target_remotes:
            remote.send(("set_attr", (attr_name, value)))
        for remote in target_remotes:
            remote.recv()

    def env_method(self, method_name, *method_args, indices=None, **method_kwargs):
        """Call instance methods of vectorized environments."""
        target_remotes = self._get_target_remotes(indices)
        for remote in target_remotes:
            remote.send(("env_method", (method_name, method_args, method_kwargs)))
        return [remote.recv() for remote in target_remotes]

    def get_envs(self):
        for idx, remote in enumerate(self.remotes):
            remote.send(("envs", None))
        return [remote.recv() for remote in self.remotes]

    def _get_indices(self, indices):
        """
        Convert a flexibly-typed reference to environment indices to an implied list of indices.

        :param indices: (None,int,Iterable) refers to indices of envs.
        :return: (list) the implied list of indices.
        """
        if indices is None:
            indices = range(self.num_envs)
        elif isinstance(indices, int):
            indices = [indices]
        return indices

    def _get_target_remotes(self, indices):
        """
        Get the connection object needed to communicate with the wanted
        envs that are in subprocesses.

        :param indices: (None,int,Iterable) refers to indices of envs.
        :return: ([multiprocessing.Connection]) Connection object to communicate between processes.
        """
        indices = self._get_indices(indices)
        return [self.remotes[i] for i in indices]

    @property
    def behaviors(self):
        return self.get_attr("behaviors")[0]

    @property
    def observation_spaces(self):
        return self.get_attr("observation_spaces")[0]

    @property
    def action_spaces(self):
        return self.get_attr("action_spaces")[0]

def _gather_subproc(obs: List[dict[str, Observation]]) -> dict[str, Observation]:
    
    combined_obs = {
            f"{key}&env={i}": value
            for i, s_obs in enumerate(obs)
            for (key, value) in s_obs.items()
        }
    return combined_obs


def _flatten_scalar(values: List[dict[str, Any]]) -> dict[str, np.ndarray]:
    keys = values[0].keys()
    return {k: np.array([v[k] for v in values]) for k in keys}


def _flatten_info(
    infos: List[dict[str, np.ndarray]]
) -> dict[str, Union[np.ndarray, List]]:
    all_metrics = {}

    all_keys = set([k for dictionary in infos for k in dictionary])
    for key in all_keys:
        if key.startswith("m_") or key.startswith("e_"):
            all_metrics[key] = np.concatenate(
                [info_i[key] for info_i in infos if key in info_i]
            )
        else:
            all_metrics[key] = [
                info_i[key] if key in info_i else None for info_i in infos
            ]

    return all_metrics



class PeekabooVectorized(SubprocVecEnv):
    
    def __post_init__(self) -> None:
        super().__post_init__()
        dummy_obs = super().reset()
        self.agents = set()
        self.agent_keys = []
        for _ in dummy_obs:
            name = parse_agent_name(_)
            self.agents.add(name["name"])
            self.agent_keys.append(_)

    def _stack_dict(self, dicts: list[dict]):
        return {
            k: np.stack((d[k] if k!="observation" else d[k][1] for d in dicts if k in d), axis=0)
            for k in dicts[0].keys() 
        }

    def _stack_list(self, dicts: list[dict]):
        return {
            k: np.stack((d[k] if k!="observation" else d[k][1] for d in dicts if k in d), axis=0)
            for k in dicts[0].keys() 
        }


    def restack(self, x):
        if isinstance( list(x.values())[0], dict):

            # can be writtern rather optimally
            return {
                agent_name: self._stack_dict([
                    v for k, v in x.items() if parse_agent_name(k)["name"] == agent_name
                ]) 
                for agent_name in self.agents
            }
        return {
            agent_name: [
                    v for k, v in x.items() if parse_agent_name(k)["name"] == agent_name
                ]
                for agent_name in self.agents
        }

    def step(self, actions):
       raw_actions = {
           agent_key: actions[parse_agent_name(agent_key)["name"]][int(parse_agent_name(agent_key)["env"])-1] for agent_key in self.agent_keys
       }
       raw_obs, raw_reward, raw_done, raw_info = super().step(raw_actions)
       return self.restack(raw_obs), self.restack(raw_reward), self.restack(raw_done)
    
    def reset(self, **kwargs) -> dict[str, Observation]:
        raw_obs = super().reset(**kwargs)
        return self.restack(raw_obs)


    
    
