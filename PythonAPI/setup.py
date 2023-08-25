import os
import sys

from setuptools import setup, find_packages
from setuptools.command.install import install

here = os.path.abspath(os.path.dirname(__file__))


# Get the long description from the README file
with open(os.path.join(here, "README.md"), encoding="utf-8") as f:
    long_description = f.read()

setup(
    name="unity-peekaboo",
    version="0.1.0",
    description="Peekaboo Unity-based Reinforcement Learning Environment",
    long_description=long_description,
    long_description_content_type="text/markdown",
    url="https://github.com/alexunderch/UnityPeekaboo",
    author="Alexander Chernyavskiy",
    classifiers=[
        "Intended Audience :: Developers",
        "Topic :: Scientific/Engineering :: Artificial Intelligence",
        "Programming Language :: Python :: 3.9",
        "Programming Language :: Python :: 3.10",
    ],
    # find_namespace_packages will recurse through the directories and find all the packages
    packages=find_packages(exclude=["*.tests", "*.tests.*", "tests.*", "tests"]),
    zip_safe=False,
    install_requires=[
        "grpcio>=1.11.0",
        "h5py>=2.9.0",
        "mlagents_envs==0.30.0",
        # Windows ver. of PyTorch doesn't work from PyPi. Installation:
        # https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Installation.md#windows-installing-pytorch
        # Torch only working on python 3.9 for 1.8.0 and above. Details see:
        # https://github.com/pytorch/pytorch/issues/50014
        'gdown', 'wheel', 'wandb==1.26.15', 'hydra-core==1.3.2',
        'mlagents==0.30.0',
        'pypiwin32==223;platform_system=="Windows"',
        "importlib_metadata==4.4; python_version<'3.8'",
    ],
    python_requires=">=3.8.13,<=3.10.12"
)