# TigerEngine: A Chess AI implemented in C#

<img align="right" src="https://github.com/TRJoseph/TigerEngine/assets/83513663/548c9647-0be7-4988-a1d9-938d09d78e18" alt="TigerEngine Logo" width="400"/>

Welcome! This repository is the host of a fully functional recreation of the classic board game, Chess! This project idea first stemmed from my love of the game of chess. I have been addicted to playing and learning about the game of chess for quite some time now and this has led to some overlap to my university studies within the computer science domain. Engine skill level has far surpassed the abilities of even the top human players of the game. After becoming proficient at the game itself and developing a thorough understanding of the rules of chess, I decided to embark upon developing an Engine of my own.

## Overview
First of all, this project is in progress and is currently receiving updates. Currently, the repository contains not only a chess computer that a player can challenge, but it also includes just the game of chess with all legal moves implemented. A human player is able to play a friend or simply mess around with the pieces. The front-end visual representation of this project is built using the Unity Game Engine. This allows for a human player to interact with the pieces. The back-end of the game is implemented using Bitboards (not entirely though). The chess board is an array of structs of length 64 (8x8 game board). Each structure within the array contains encoded information about the current piece occupying the square as well as pre-calculated distances to the edge of the board from each square. This information is useful for determining when to stop the move search once the algorithm reaches either another piece occupying a square or the edge of the game board. Swapping entirely over to Bitboards for this project is coming in the future to improve Engine efficiency as a full bitboard implementation would allow for more efficient legal move calculations using binary bitwise operations.

## Current Features
As previously stated, this project contains the game of chess implemented in full. This includes special moves such as castling, en passant, and pawn promotions. These are all available to the player when the move is legal in the given position. As of commit [`c074cae`](https://github.com/TRJoseph/TigerEngine/commit/c074caedde3fd045cd098068dcbba9164b79d0f7), a foundational layer is placed for engine development. Currently the Engine plays legal moves completely at random. The next steps are to of course implement a more advanced AI. One potential method in doing this is to implement the minimax algorithm and custom evaluation functions to determine whether the position is advantageous for white or black.

## Some In-Progress Features
- A main menu for the user to select which side they would like to play as
- Volume feedback for specific moves (castling, check, checkmate)

## Play the Game
Once the first release is available this will be updated. The project needs a more intuitive interface first (main menu, exit game button)

## For Developers
This section is mainly reserved for my friends at university that have also shown interest in this project and may want to help out. Head over to the **Contributing** page to see how to get started.

**🚀 [Contributing](CONTRIBUTING.md)**
