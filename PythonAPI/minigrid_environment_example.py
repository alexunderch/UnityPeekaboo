from typing import Optional
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.envs.unity_parallel_env import UnityParallelEnv
import os
from pathlib import Path


#
# This wrapper is designed only for parallel pettingZoo API to satisfy POSG

class MiniGridEnv(UnityParallelEnv):
    def __init__(self, executable_path: Path, seed: int | None = None, no_grahics: bool = True):
        """
        Notice: Currently communication between Unity and Python takes place over an open socket without authentication. 
        Ensure that the network where training takes place is secure.

        Notice: development build only //TODO: fix
        """
        self._worker_id: int = 0
        unity_env = UnityEnvironment(file_name=executable_path, 
                               worker_id=self._worker_id, 
                               seed=seed,
                               no_graphics=no_grahics)
        
        super().__init__(unity_env, seed)

        #TODO: auxillary stuff to work with python training more smoothly



def test():
    executable_path = "../Executables/DevRelease/dev_release.x86_64"

    env = MiniGridEnv(executable_path=executable_path, seed=42, no_grahics=True)
    print("Action space:", env.action_spaces)
    print("Observation space:", env.observation_spaces)
    print("Reset Info\n", env.reset())    
    prev_observe = env.reset()
    
    for iter in range(env.num_agents * 10):
        actions = {agent: env.action_space(agent).sample() for agent in env._agents}
        prev_observe, reward, done, info  = env.step(actions)

    env.close()

if __name__ == "__main__":
    test()