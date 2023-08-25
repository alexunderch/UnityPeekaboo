# Python API for the environment

## Installation 
>[Warning] not well tested.

```
pip install -e .
```

## Structural description

```Bash
├── baselines
│   ├── clean_mappo_baseline.py #mappo baseline
│   ├── random_agents.py #random agents baseline
├── peekaboo_environment.py #environment class
├── setup.py
└── test_peekaboo_environment.py
```

To test that everything is alright:
```Bash
python test_peekaboo_environment.py
```

## Baselines

### Random agents
```Bash
cd baselines;
python random_agents.py
```
### MAPPO 
This baseline works with [`wandb`](https://wandb.ai/) and [`hydra`](https://hydra.cc/) to build experiment workflow.
```Bash
export WANDB_API_KEY=your_magical_key
```

```Bash
cd baselines;
python clean_mappo_baseline.py #all hydra staff u want
```

Hydra config could be found [here](../configs/python/mappo_config.yaml) and looks like this:
```yaml
    environment:
      environment_executable: str #path from the "./baselines/" directory
      seed: int
      render: bool
    
    agent:
      agent_hidden_dim: int
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

      seed: int
      torch_deterministic: bool

      lr: float
      anneal_lr: bool
      device: str
      max_grad_norm: float
      target_kl: float

    wandb:
      track_wandb: bool #true
      wandb_project_name: str
      wandb_run_name: str
```
