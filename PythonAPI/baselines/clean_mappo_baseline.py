import os
import sys
import time
import copy
import random
from typing import Callable, Iterable, Any
from dataclasses import dataclass
 
import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F 
 
import wandb
import hydra
from omegaconf import DictConfig, OmegaConf

sys.path.append("..")
from peekaboo_environment import PeekabooEnv

def init_layer(layer: nn.Module, std: float=np.sqrt(2), bias_const: float=0.0):        
    torch.nn.init.orthogonal_(layer.weight, std)
    torch.nn.init.constant_(layer.bias, bias_const)
    return layer

def print_dict(d: dict) -> None:
    for k,v  in d.items():
        print(f"{k}: \t {v}")

def dict_to_array(d: dict):
    return np.array(list(d.values()))

def array_to_dict(arr: np.ndarray, keys: Iterable):
    assert len(arr) == len(keys)

    return {
        key: arr_el for key, arr_el in zip(keys, arr)
    }

def flatten_list(l: list) -> list:
    return [item for sublist in l for item in sublist]

def calc_grad_norm(parameters: Iterable):
    grads = [
        param.grad.detach().flatten()
        for param in parameters
        if param.grad is not None
    ]
    norm = torch.cat(grads).norm()
    return norm.item()

class CategoricalMasked(torch.distributions.Categorical):
    def __init__(self, probs=None, logits=None, validate_args=None, masks=[]):
        self.masks = masks
        self.masking_constant = torch.tensor(-1e8)
        if len(self.masks) == 0:
            super().__init__(probs, logits, validate_args)
        else:
            self.masks = masks.type(torch.BoolTensor).to(logits.device)
            #masking branches accordingly
            logits = torch.where(self.masks, logits, self.masking_constant.to(logits.device))
            super().__init__(probs, logits, validate_args)
 
    def entropy(self):
        if len(self.masks) == 0:
            return super(CategoricalMasked, self).entropy()
        p_log_p = self.logits * self.probs
        p_log_p = torch.where(self.masks, p_log_p, torch.tensor(0.0).to(self.logits.device))
        return -p_log_p.sum(-1)
 
 
class CriticNetwork(nn.Module):
    def __init__(self, state_dim: int, hidden_dim: int) -> None:
        super().__init__()
        self.critic_network = nn.Sequential(
            init_layer(nn.Linear(state_dim, hidden_dim)),
            nn.ReLU(),
            init_layer(nn.Linear(hidden_dim, hidden_dim)),
            nn.ReLU(),
            init_layer(nn.Linear(hidden_dim, 1), std=1)
        )
 
    def forward(self, state: torch.Tensor) -> torch.Tensor:
        return self.critic_network(state)
 
 
class MultiDiscreteActorNetwork(nn.Module):
    """
    An agent that works with MultiDiscrete action space
    """
    def __init__(self, state_dim: int, hidden_dim: int, action_space) -> None:
        super().__init__()
        self.action_space = action_space
        self.actor_network = nn.Sequential(
            init_layer(nn.Linear(state_dim, hidden_dim)),
            nn.ReLU(),
            init_layer(nn.Linear(hidden_dim, hidden_dim)),
            nn.ReLU(),
            init_layer(nn.Linear(hidden_dim, self.action_space.sum()), std=1)
        )

 
    def forward(self, state: torch.Tensor, action_mask: torch.Tensor = None, action: torch.Tensor | None=None) -> torch.Tensor:
        logits = self.actor_network(state)
        split_logits = torch.split(logits, self.action_space.tolist(), dim=-1)
        
        if action_mask[0] is not None:
            split_action_masks = [torch.BoolTensor(am) for am in action_mask[0]]
            multi_categoricals = [
                CategoricalMasked(logits=logits, masks=iam) for (logits, iam) in zip(split_logits, split_action_masks)
            ]
        else:
            multi_categoricals = [torch.distributions.Categorical(logits=logits) for logits in split_logits]
        if action is None:
            action = torch.stack([categorical.sample() for categorical in multi_categoricals])
        
        # print(action.shape, multi_categoricals)
        logprob = torch.stack([categorical.log_prob(a) for a, categorical in zip(action, multi_categoricals)])
        entropy = torch.stack([categorical.entropy() for categorical in multi_categoricals])
        return action.T, logprob.sum(0), entropy.sum(0)

class DiscreteActorNetwork(nn.Module):
    """
    An agent that works with MultiDiscrete action space
    """
    def __init__(self, state_dim: int, hidden_dim: int, action_space) -> None:
        super().__init__()
        self.action_space = action_space
        self.actor_network = nn.Sequential(
            init_layer(nn.Linear(state_dim, hidden_dim)),
            nn.ReLU(),
            init_layer(nn.Linear(hidden_dim, hidden_dim)),
            nn.ReLU(),
            init_layer(nn.Linear(hidden_dim, self.action_space), std=1)
        )

 
    def forward(self, state: torch.Tensor, action_mask: torch.Tensor = None, action: torch.Tensor | None=None) -> torch.Tensor:
        logits = self.actor_network(state)
        categorical = torch.distributions.Categorical(logits=logits)
        if action is None:
            action = categorical.sample()
        
        # print(action.shape, multi_categoricals)
        logprob = categorical.log_prob(action)
        entropy = categorical.entropy()
        return action.T, logprob.sum(0), entropy.sum(0)




class ActorCriticAgent(nn.Module):
    def __init__(self,
                 agent_id: str,
                 environment: PeekabooEnv,
                 hidden_dim: int,
                 cent_critic_state_dim: int | None = None) -> None:
        super().__init__()
    
        state_dim = environment.observation_space(agent=agent_id).shape[0]
        action_dims = environment.action_space(agent=agent_id).nvec
        self.actor_network = MultiDiscreteActorNetwork(state_dim=state_dim, hidden_dim=hidden_dim, action_space=action_dims)
        
        self.shared_critic = cent_critic_state_dim is not None
        state_dim = cent_critic_state_dim if self.shared_critic else state_dim
        self.critic_network = CriticNetwork(state_dim=state_dim, hidden_dim=hidden_dim)

    def get_value(self, state: torch.Tensor, cent_state: torch.Tensor | None = None):
        return self.critic_network(state if not self.shared_critic else cent_state)

    
    def get_action(self, state: torch.Tensor,  action_mask: torch.Tensor | None = None, action: torch.Tensor | None=None):
        env_action, logprob, entropy = self.actor_network(state, action_mask=action_mask, action=action)
        return env_action, logprob, entropy
    
    def get_action_and_value(self, state: torch.Tensor, 
                                   cent_state: torch.Tensor | None = None,
                                   action_mask: torch.Tensor | None = None, 
                                   action: torch.Tensor | None=None):
        value = self.get_value(state, cent_state)
        env_action, logprob, entropy = self.get_action(state, action_mask=action_mask, action=action)
        return value, env_action, logprob, entropy



@dataclass
class MAPPOAgent:
    config: dict
    agent_hidden_dim: int

    environment: PeekabooEnv
    shared_critic: bool
    shared_optimization: bool 

    ent_coeff: float 
    vf_coeff: float 
    clip_coeff: float 
    gamma: float 
    gae: bool 
    gae_lambda: float
    clip_vloss: bool 

    batch_size: int 
    num_minibatches: int 
    stack_size: int #not used

    total_timesteps: int 
    num_learning_epochs: int
    num_steps: int 
    num_eval_steps: int 
    num_estimations: int # not used

    seed: int | None 
    torch_deterministic: bool 

    lr: float 
    anneal_lr: bool 
    device: str
    max_grad_norm: float 
    target_kl: float | None 

    track_wandb: bool 
    wandb_project_name: str | None = None
    wandb_run_name: str | None = None


    def __post_init__(self):
        self.device = torch.device(self.device)
        self.possible_agents = self.environment.possible_agents 
        self.num_agents = len(self.possible_agents)

        self.centralized_observation_shape = np.sum([self.environment.observation_space(agent_id).shape[0] for agent_id in self.possible_agents]) 
        self.agents = [
            ActorCriticAgent(agent_id=agent_id, 
                             environment=self.environment, 
                             hidden_dim=self.agent_hidden_dim, 
                             cent_critic_state_dim=self.centralized_observation_shape if self.shared_critic else None).to(self.device) 
            for agent_id in self.possible_agents
            ]
        self.optimizers = [torch.optim.AdamW(self.agents[i].parameters(), lr=self.lr, eps=1e-5) for i in range(self.num_agents)]


        random.seed(self.seed)
        np.random.seed(self.seed)
        torch.manual_seed(self.seed)
        torch.backends.cudnn.deterministic = self.torch_deterministic

        if self.track_wandb:
            wandb.init(
                            project=self.wandb_project_name,
                            name=self.wandb_run_name,
                            save_code=True,
                            config=self.config
                        )

    def learn(self):
        agent_ids = self.possible_agents

        start_time = time.time()
        num_updates = self.total_timesteps

       
        for update in range(1, num_updates+1):
            episodic_return = np.zeros((self.num_agents))

            next_obs = self.environment.reset()
            next_done = self.environment.dones

            next_obs = [
                torch.Tensor(next_obs[agent_id]['observation']).to(self.device)
                for agent_id in agent_ids
            ]       

            try:
                next_action_mask = [
                    next_obs[agent_id]['action_mask']
                    for agent_id in agent_ids
                ]
            except TypeError:
                next_action_mask = [None] * self.num_agents       

            next_done = torch.from_numpy(dict_to_array(next_done)).to(self.device)


            rb_obs = [[] for _ in range(self.num_steps)]
            rb_actions = [[] for _ in range(self.num_steps)]
            rb_action_masks = [[] for _ in range(self.num_steps)]

            rb_logprobs = [[] for _ in range(self.num_steps)]

            rb_cent_obs = torch.zeros((self.num_steps, self.centralized_observation_shape)).to(self.device)

            rb_rewards = torch.zeros((self.num_steps, len(agent_ids))).to(self.device)
            rb_terms =  torch.zeros((self.num_steps, len(agent_ids))).to(self.device)
            rb_values =  torch.zeros((self.num_steps, len(agent_ids))).to(self.device)
            rb_entropies = [[] for _ in range(self.num_steps)]

            if self.anneal_lr:
                frac = 1.0 - (update - 1.0) / num_updates
                lrnow = frac * self.lr

                for agent_ind in range(self.num_agents):    
                    self.optimizers[agent_ind].param_groups[0]["lr"] = lrnow

            for collection_step in range(self.num_steps):          
                rb_cent_obs[collection_step] = torch.cat(next_obs, dim=-1).to(self.device) 
                rb_obs[collection_step] = next_obs 
                rb_action_masks[collection_step] = next_action_mask
                rb_terms[collection_step] = next_done
                for agent_ind in range(self.num_agents):
                    with torch.no_grad():
                        value, action, logprob, entropy = self.agents[agent_ind].get_action_and_value(next_obs[agent_ind],
                                                                                                      cent_state=rb_cent_obs[collection_step] if self.shared_critic else None,
                                                                                                      action_mask=next_action_mask)
                        rb_values[collection_step] = value.flatten()

                    rb_actions[collection_step].append(action.flatten())
                    rb_logprobs[collection_step].append(logprob.flatten())
                    rb_entropies[collection_step].append(entropy.flatten())
                
                numpy_actions = [a.detach().cpu().tolist() for a in rb_actions[collection_step]]
                tmp_next_obs, reward, tmp_done, info = self.environment.step(array_to_dict(numpy_actions, agent_ids))

                reward_arr = dict_to_array(reward)
                rb_rewards[collection_step] = torch.from_numpy(reward_arr).to(self.device)
                episodic_return += reward_arr

                try:
                    next_obs = [
                            torch.Tensor(tmp_next_obs[agent_id]['observation']).to(self.device)
                            for agent_id in agent_ids
                        ]
                    
                    next_action_mask = [
                            tmp_next_obs[agent_id]['action_mask']
                            for agent_id in agent_ids
                        ]
                    
                except IndexError:
                    next_obs = [
                            torch.Tensor(tmp_next_obs[agent_id]).to(self.device)
                            for agent_id in agent_ids
                        ]
                    
                    next_action_mask = [None] * self.num_agents
                    
                next_done = torch.from_numpy(dict_to_array(tmp_done)).to(self.device)

                episode_length = collection_step + update
                if torch.any(next_done):
                    break
                
                collection_logs = {"episode length": episode_length,
                                  "episode return": array_to_dict(episodic_return, agent_ids)}
                if self.track_wandb:
                    wandb.log(collection_logs)
                else: 
                    print_dict(collection_logs)

            
            with torch.no_grad():
                
                next_value = torch.stack(
                    [
                        self.agents[agent_ind].get_value(next_obs[agent_ind], 
                                                         cent_state=rb_cent_obs[collection_step] if self.shared_critic else None).flatten() 
                        for agent_ind in range(self.num_agents)
                    ]
                )

                next_value = next_value.reshape(1, -1).to(self.device)
                                
                if self.gae:
                    advantages = torch.zeros_like(rb_rewards).to(self.device)
                    lastgaelam = 0
                    for t in reversed(range(self.num_steps)):
                        if t == self.num_steps - 1:
                            nextnonterminal = ~next_done
                            nextvalues = next_value
                        else:
                            nextnonterminal = 1.0 - rb_terms[t + 1]
                            nextvalues = rb_values[t + 1]
                        delta = rb_rewards[t] + self.gamma * nextvalues * nextnonterminal - rb_values[t]
                        advantages[t] = lastgaelam = delta + self.gamma * self.gae_lambda * nextnonterminal * lastgaelam
                    returns = advantages + rb_values
                else:
                    returns = torch.zeros_like(rb_rewards).to(self.device)
                    for t in reversed(range(self.num_steps)):
                        if t == self.num_steps - 1:
                            nextnonterminal = ~next_done
                            next_return = next_value
                        else:
                            nextnonterminal = 1.0 - rb_terms[t + 1]
                            next_return = returns[t + 1]
                        returns[t] = rb_rewards[t] + self.gamma * nextnonterminal * next_return
                    advantages = returns - rb_values

            b_obs = torch.stack(flatten_list(rb_obs))
            b_cent_obs = rb_cent_obs.repeat(self.num_agents, 1)
            b_action_masks = flatten_list(rb_action_masks)

            b_logprobs = torch.stack(flatten_list(rb_logprobs))
            b_actions = torch.stack(flatten_list(rb_actions))
        
            b_advantages = advantages.reshape(-1)
            b_returns = returns.reshape(-1)
            b_values = rb_values.reshape(-1)

            # Optimizing the policy and value network
            b_inds = np.arange(min(self.batch_size, len(b_obs)))
            clipfracs = []
            for learning_epoch in range(self.num_learning_epochs):
                np.random.shuffle(b_inds)
                
                minibatch_size  = len(b_inds)//self.num_minibatches
                policy_losses = np.zeros((self.num_minibatches+2, len(agent_ids)))
                value_losses = np.zeros((self.num_minibatches+2, len(agent_ids)))
                entropy_losses = np.zeros((self.num_minibatches+2, len(agent_ids)))
                losses = np.zeros((self.num_minibatches+2, len(agent_ids)))
                approx_kls = np.zeros((self.num_minibatches+2, len(agent_ids)))

                clipfracs_per_agent = []
                for start in range(0, len(b_inds), minibatch_size):
                    end = start + minibatch_size
                    mb_inds = b_inds[start:end]
                    minibatch_ind = start // minibatch_size

                    for agent_ind in range(self.num_agents):
                        newvalue, _, newlogprob, entropy= self.agents[agent_ind].get_action_and_value(b_obs[mb_inds].squeeze().to(self.device), 
                                                                                                     cent_state=b_cent_obs[mb_inds].squeeze() if self.shared_critic else None,
                                                                                                     action_mask=[b_action_masks[ind] for ind in mb_inds], 
                                                                                                     action=b_actions.long()[mb_inds].T.to(self.device))
                        logratio = newlogprob - b_logprobs[mb_inds].squeeze(-1).to(self.device)
                        ratio = logratio.exp()

                        with torch.no_grad():
                            # calculate approx_kl http://joschu.net/blog/kl-approx.html
                            # old_approx_kl = (-logratio).mean()
                            approx_kl = ((ratio - 1) - logratio).mean()
                            approx_kls[minibatch_ind][agent_ind] = approx_kl
                            clipfracs_per_agent += [((ratio - 1.0).abs() > self.clip_coeff).float().mean().item()]
                    
                        mb_advantages = b_advantages[mb_inds]
                        mb_advantages = (mb_advantages - mb_advantages.mean()) / (mb_advantages.std() + 1e-8)

                        # Policy loss
                
                        pg_loss1 = -mb_advantages * ratio
                        pg_loss2 = -mb_advantages * torch.clamp(ratio, 1 - self.clip_coeff, 1 + self.clip_coeff)
                        pg_loss = torch.max(pg_loss1, pg_loss2).mean()
                        
                        policy_losses[minibatch_ind][agent_ind] = pg_loss.item()
                        # Value loss
                        newvalue = newvalue.flatten()
                        if self.clip_vloss:
                            v_loss_unclipped = (newvalue - b_returns[mb_inds]) ** 2
                            v_clipped = b_values[mb_inds] + torch.clamp(
                                newvalue - b_values[mb_inds],
                                -self.clip_coeff,
                                self.clip_coeff,
                            )
                            v_loss_clipped = (v_clipped - b_returns[mb_inds]) ** 2
                            v_loss_max = torch.max(v_loss_unclipped, v_loss_clipped)
                            v_loss = 0.5 * v_loss_max.mean()
                        else:
                            v_loss = 0.5 * ((newvalue - b_returns[mb_inds]) ** 2).mean()
                        
                        value_losses[minibatch_ind][agent_ind] =v_loss.item()

                        entropy_loss = entropy.mean()
                        entropy_losses[minibatch_ind][agent_ind] = entropy_loss.item()

                        loss = pg_loss - self.ent_coeff * entropy_loss + v_loss * self.vf_coeff
                        losses[minibatch_ind][agent_ind] = loss.item()

                        self.optimizers[agent_ind].zero_grad()
                        loss.backward()
                        nn.utils.clip_grad_norm_(self.agents[agent_ind].parameters(), self.max_grad_norm)
                        self.optimizers[agent_ind].step()
                
                clipfracs += clipfracs_per_agent
                

                y_pred, y_true = b_values.cpu().numpy(), b_returns.cpu().numpy()
                var_y = np.var(y_true)
                explained_var = np.nan if var_y == 0 else 1 - np.var(y_true - y_pred) / var_y

                training_logs = {
                                "epoch": learning_epoch,
                                "lr": self.optimizers[agent_ind].param_groups[0]["lr"],
                                f"grad norm/{self.possible_agents[agent_ind]}": calc_grad_norm(self.agents[agent_ind].parameters()),
                                "losses/value loss": array_to_dict(value_losses.mean(0), agent_ids),
                                "losses/pg loss": array_to_dict(policy_losses.mean(0), agent_ids),
                                "losses/entropy loss": array_to_dict(entropy_losses.mean(0), agent_ids),
                                "losses/overall loss": array_to_dict(losses.mean(0), agent_ids),
                                "losses/approx_kl": array_to_dict(approx_kls.mean(0), agent_ids),
                                "losses/expalined var": explained_var,
                                "time per step": time.time() - start_time
                                }

                if self.track_wandb:
                    wandb.log(training_logs)
                else: 
                    print_dict(training_logs)

                if self.target_kl is not None:
                    if approx_kl > self.target_kl:
                        break

        self.environment.close()

    @torch.no_grad()
    def eval(self):
        agent_ids = self.possible_agents

        episodic_return = np.zeros((self.num_agents))

        next_obs = self.environment.reset()
        next_done = self.environment.dones

        next_obs = [
            torch.Tensor(next_obs[agent_id]['observation']).to(self.device)
            for agent_id in agent_ids
        ]       

        try:
            next_action_mask = [
                next_obs[agent_id]['action_mask']
                for agent_id in agent_ids
            ]
        except TypeError:
            next_action_mask = [None] * self.num_agents       

        next_done = torch.from_numpy(dict_to_array(next_done)).to(self.device)


        rb_obs = [[] for _ in range(self.num_eval_steps)]
        rb_actions = [[] for _ in range(self.num_eval_steps)]
        rb_action_masks = [[] for _ in range(self.num_eval_steps)]

        rb_logprobs = [[] for _ in range(self.num_eval_steps)]

        rb_cent_obs = torch.zeros((self.num_eval_steps, self.centralized_observation_shape)).to(self.device)

        rb_rewards = torch.zeros((self.num_eval_steps, len(agent_ids))).to(self.device)
        rb_terms =  torch.zeros((self.num_eval_steps, len(agent_ids))).to(self.device)
        rb_values =  torch.zeros((self.num_eval_steps, len(agent_ids))).to(self.device)
        rb_entropies = [[] for _ in range(self.num_eval_steps)]

        for collection_step in range(self.num_eval_steps):          
            rb_cent_obs[collection_step] = torch.cat(next_obs, dim=-1).to(self.device) 
            rb_obs[collection_step] = next_obs 
            rb_action_masks[collection_step] = next_action_mask
            rb_terms[collection_step] = next_done
            for agent_ind in range(self.num_agents):
                value, action, logprob, entropy = self.agents[agent_ind].action_and_value(next_obs[agent_ind],
                                                                                          action_mask=next_action_mask)
                rb_values[collection_step] = value.flatten()

                rb_actions[collection_step].append(action.flatten())
                rb_logprobs[collection_step].append(logprob.flatten())
                rb_entropies[collection_step].append(entropy.flatten())

        
            numpy_actions = [a.detach().cpu().tolist() for a in rb_actions[collection_step]]
            tmp_next_obs, reward, tmp_done, info = self.environment.step(array_to_dict(numpy_actions, agent_ids))

            reward_arr = dict_to_array(reward)
            rb_rewards[collection_step] = torch.from_numpy(reward_arr).to(self.device)
            episodic_return += reward_arr

            try:
                next_obs = [
                        torch.Tensor(tmp_next_obs[agent_id]['observation']).to(self.device)
                        for agent_id in agent_ids
                    ]
                
                next_action_mask = [
                        tmp_next_obs[agent_id]['action_mask']
                        for agent_id in agent_ids
                    ]
                
            except IndexError:
                next_obs = [
                        torch.Tensor(tmp_next_obs[agent_id]).to(self.device)
                        for agent_id in agent_ids
                    ]
                
                next_action_mask = [None] * self.num_agents
                
            next_done = torch.from_numpy(dict_to_array(tmp_done)).to(self.device)

            episode_length = collection_step 
            if torch.any(next_done):
                break
            
            collection_logs = {"evaluation/episode length": episode_length,
                                "evaluation/episode return": array_to_dict(episodic_return, agent_ids)}
            if self.track_wandb:
                wandb.log(collection_logs)
            else: 
                print_dict(collection_logs)

            self.environment.close()

@hydra.main(version_base=None, config_path="../../configs/python", config_name="mappo_config")
def main(config: DictConfig):
    env_config = config.environment
    
    environment = PeekabooEnv(
        env_config.environment_executable, env_config.seed, not env_config.render, 1
    )
    agent_args = OmegaConf.to_container(config.agent, resolve=True)
    wandb_args = OmegaConf.to_container(config.wandb, resolve=True)
    learner = MAPPOAgent(environment=environment,
                         config=agent_args,
                         **agent_args, **wandb_args)
    
    learner.learn()
    learner.eval()


if __name__ == "__main__":
    main()
