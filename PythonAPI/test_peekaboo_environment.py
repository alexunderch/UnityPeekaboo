

from peekaboo_environment import PeekabooEnv

def test():
    executable_path = "../Executables/DevRelease/dev_release.x86_64"

    env = PeekabooEnv(executable_path=executable_path, seed=42, no_grahics=True)
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