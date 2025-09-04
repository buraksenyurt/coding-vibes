
# Walkure Game Design Document

This document contains the requirements, game mechanics, and technical details for the development of the tank battle and strategy game named Walkure.

## 1. General Information

- The game will be developed using the Go programming language.
- All source code will be collected under the `src` folder.
- Game assets (images) will be in the `assets` folder, and music and sounds will be in the `sounds` folder.
- The game will only run on the Windows platform.
- The game consists of a single scene. Screen size: 900x400 pixels.

## 2. Gameplay and Objective

The player controls a tank and tries to collect gold bonuses that appear randomly on the scene. The goal is to collect as much gold as possible without hitting mines or getting stuck on obstacles. The game ends when the tank's armor drops to zero, and the number of collected gold coins becomes the player's score.

## 3. Controls

- **Space**: Moves the tank forward.
- **Left Arrow**: Rotates the tank to the right around its own axis.
- **Right Arrow**: Rotates the tank to the left around its own axis.

## 4. Game Mechanics

- At the start of the game, the tank appears in the lower left corner of the scene.
- There can be a maximum of 5 mines (`Mine.png`) on the scene at the same time.
- Each mine's explosion time is randomly set between 10-30 seconds.
- 5 seconds after a mine explodes, a new mine is added to a random location on the scene. Mines cannot overlap.
- When a mine explodes, the images in the `Bomb` folder are played in sequence to show the explosion animation (from `Bomb_Explosion_A_001` to `Bomb_Explosion_A_007`).
- At the start of the game, 10 gold bonuses are randomly placed on the scene. Each time a gold is collected, a new gold appears at a random point. Golds are represented by `Coin_A.png`.
- The game ground is created with random motifs from the `Ground` folder.
- The tank's armor starts at 100 units. When a mine explodes, if the distance between the tank and the mine is less than 50 pixels, the tank takes 10 damage.
- The game ends when the tank's armor drops to 0.
- There are 4 `Czech_Hdgehog_A` obstacles randomly placed on the scene. The tank cannot pass through these obstacles.

## 5. Game Loop

1. The game starts, and the tank and mines are placed on the scene.
2. The player tries to collect gold by directing the tank.
3. Mines explode at certain intervals and new ones are added.
4. If the tank's armor drops to zero, the game ends.
5. The score is displayed on the screen, and a new game can be started.

## 6. Score and Difficulty

- Each collected gold is worth 1 point.
- As the game progresses, the explosion times of the mines can be shortened to increase difficulty.

## 7. Technical Requirements

- The game runs with a single main loop (game loop).
- Visual and sound files are loaded directly from the relevant folders.
- The game should be optimized for a target of 60 FPS.

## 8. Asset Usage

- The tank consists of the images `Gun_01`, `Hull_01`, and `Track_1`.
- The `Exhaust_Fire` effect is shown when the tank moves.
- `Ground_Tile_01` and `Ground_Tile_02` are used for the ground.
- `Czech_Hdgehog_A` is used for obstacles.
- Images in the `Bomb` folder are used for mine and explosion effects.
- `Coin_A` is used for the gold bonus.
- The game music is the `MainTitle_Wagner_Walkure` file in the `Sounds` folder.

## 9. Development Notes

- Code should be written in a modular and readable way.
- Game logic, visual, and sound management should be designed as separate modules.

---
This document summarizes the basic design and development requirements of the Walkure game. Additional requirements and improvements may be made during the development process.
