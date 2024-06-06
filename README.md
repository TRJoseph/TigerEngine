# TigerEngine: A Chess AI implemented in C#

<img align="right" src="https://github.com/TRJoseph/TigerEngine/assets/83513663/548c9647-0be7-4988-a1d9-938d09d78e18" alt="TigerEngine Logo" width="400"/>

Welcome! This repository is the host of a fully functional recreation of the classic board game, Chess! This project idea first stemmed from my love of the game of chess. I have been addicted to playing and learning about the game of chess for quite some time now and this has led to some overlap to my university studies within the computer science domain. Engine skill level has far surpassed the abilities of even the top human players of the game. After becoming proficient at the game itself and developing a thorough understanding of the rules of chess, I decided to embark upon developing an Engine of my own.

## Overview

First of all, this repository is the host of three subprojects. The main UCIEngine (Universal Chess Protocol) project is in progress and is currently receiving updates. Currently, the repository contains not only a chess computer that a player can challenge, but it also includes just the game of chess with all legal moves implemented. A human player is able to play a friend or simply mess around with the pieces. The front-end visual representation of this project is built using the Unity Game Engine. This allows for a human player to interact with the pieces. The back-end of the game is implemented using Bitboards and various other techniques. The EngineMatchupApp is a folder containing another sub-project that is purely for debugging and benchmarking purposes. This application allows me to pit one version of the UCIEngine versus a previous version to ensure, over a large sample size of differing game positions, that the new version is actually playing better Chess than previous iterations.

## Current Features

As previously stated, this project contains the game of chess implemented in full. This includes special moves such as castling, en passant, and pawn promotions. These are all available to the player when the move is legal in the given position. Commit [`fc4ff59`](https://github.com/TRJoseph/TigerEngine/commit/fc4ff594bdd785ca85723f621a424c112874723e) includes a fully functional AI adhering to the Universal Chess Protocol Specifications, a benchmarking application, as well as a standalone unity app allowing a user to play the engine completely offline. More information about each of these sub-projects can be found in their respective READMEs located within each project's directory.

## Some In-Progress Features

- A main menu for the user to select which side they would like to play as
- Volume feedback for specific moves (castling, check, checkmate)

## Play the Game

Once the first release is available this will be updated. The project needs a more intuitive interface first (main menu, exit game button)

## For Developers

This section is mainly reserved for my friends at university that have also shown interest in this project and may want to help out. Head over to the **Contributing** page to see how to get started.

**🚀 [Contributing](CONTRIBUTING.md)**
