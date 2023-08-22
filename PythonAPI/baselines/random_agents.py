
#TODO absolute imports
import sys 
sys.path.append("..")

from peekaboo_environment import PeekabooEnv
from time import time
import numpy as np
from dataclasses import dataclass
"""
The most simple baseline to test logging procedures. It does not use any learning routine.
The script serves to check that the environment mechanics works properly.
"""


class RandomAgent:
    """
    Processes observations from a single env
    """
    def __init__(self):
        self.actions_space = None

    def set_action_space(self, action_space):
        self.actions_space = action_space

    def act(self, agent: str, observation: dict):
        """ 
            Input a single observation 
            Return action, user_termination
                user_termination - Return True to terminate the episode
        """
        action = self.actions_space(agent).sample()
        return action

@dataclass
class ExperimentConfig:
    n_epochs: int = 1000
    executable_path: str = "../../Executables/dev_release_fouroom.x86_64"
    render_env: bool = False
    seed = 1342

def run_experiment():
    config = ExperimentConfig()
    environment = PeekabooEnv(config.executable_path, 
                              seed=config.seed, 
                              no_grahics=not config.render_env)

    print("Action space:", environment.action_spaces)
    print("Observation space:", environment.observation_spaces)

    observation = environment.reset()
    print(observation)
 
    pyagent = RandomAgent()
    pyagent.set_action_space(environment.action_space) 

    rewards = {agent: [] for agent in environment.agents}

    # print( environment.action_spaces[environment.agents[0]])     

    for iter in range(config.n_epochs):
        episode_time = time()
        for _ in environment.agents:
            # this is where you would insert your policy
            actions = {agent: pyagent.act(agent, observation) for agent in environment.agents}
            observation, reward, done, info = environment.step(actions)
            for a, r in reward.items():
                rewards[a].append(r)
    
            if any(list(done.values())):
                break
            
        print(f"After iter {iter+1} mean reward = {dict({k:np.mean(v) for k, v in rewards.items()})}")            
        print(f"It took {time() - episode_time}s")        

    environment.close()

if __name__ == "__main__":
    run_experiment()
