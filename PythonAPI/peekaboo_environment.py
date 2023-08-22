from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.envs.unity_parallel_env import UnityParallelEnv
import os
from pathlib import Path


#
# This wrapper is designed only for parallel pettingZoo API to satisfy POSG

class PeekabooEnv(UnityParallelEnv):
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


