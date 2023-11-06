from peekaboo_environment import PeekabooEnv, PeekabooVectorized, create_env_factory, SubprocVecEnv
import time
def test():
    # executable_path = "../Executables/DevRelease/dev_release.x86_64"
    executable_path = "C:\\Users\\Alexander\\Documents\\UnityPeekaboo\\Executables\\pogema_example\\UnityPeekaboo.exe"

    env = PeekabooEnv(executable_path=executable_path, seed=42, no_grahics=True, worker_id=12)
    print("Action space:", env.action_spaces)
    print("Observation space:", env.observation_spaces)
    print("Reset Info\n", env.reset())    
    prev_observe = env.reset()
    
    for iter in range(env.num_agents * 10):
        actions = {agent: env.action_space(agent).sample() for agent in env._agents}
        prev_observe, reward, done, info  = env.step(actions)

    env.close()


    env_kwargs = dict(executable_path=executable_path, seed=42)
    
    for n_proc in [2]:
        st = time.time()
        env = PeekabooVectorized(
            env_fns=[
                create_env_factory(worker_id=i, env_kwargs=env_kwargs) for i in range(n_proc)
            ]
        )

        print("Action space:", env.action_spaces)
        print("Observation space:", env.observation_spaces)
        for iter in range(2):
            # print([agent.split("&")[0]for agent in vectorized_keys])
            actions = {agent: n_proc*[env.action_space[agent.split("&")[0]].sample()] for agent in env.agents}
            obs, reward, done  = env.step(actions)

            print(obs)

            print(reward)

            print(done)
        
        env.close()
        print(f"with {n_proc} processes it has { n_proc / (time.time() - st) * 100} sps")

if __name__ == "__main__":
    test()