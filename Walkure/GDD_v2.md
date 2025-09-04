# Walkure - Game Design Document v2.0

> This document was written by Claude Sonnet 4.0

**Development Platform:** Go (Golang) + Ebiten Game Engine  
**Target Platform:** Windows  
**Development Process:** AI-Assisted Development with Grok Code Fast 1 (Preview)  

---

## 1. Game Overview

**Walkure** is a 2D top-down tank battle and survival game. The player controls a tank to collect gold coins while avoiding mine explosions. The goal is to collect as many gold coins as possible before the tank's armor runs out.

### 1.1 Game Genre
- **Primary Category:** Action/Arcade
- **Sub Categories:** Survival, Collection, Top-down Shooter
- **Target Audience:** Casual gamers, retro game enthusiasts

---

## 2. Technical Specifications

### 2.1 Platform and Technology
- **Programming Language:** Go (Golang) 1.21+
- **Game Engine:** Ebiten v2.7.3
- **Target Platform:** Windows (testable in Linux development environment)
- **Resolution:** 900x400 pixels (fixed)
- **Target FPS:** 60 FPS

### 2.2 Performance Criteria
- **Target FPS:** Stable 60 FPS
- **Memory Usage:** <100MB RAM
- **Startup Time:** <3 seconds
- **Asset Loading:** All assets loaded at game startup

### 2.3 File Structure
```
Walkure/
├── src/
│   ├── main.go              # Main game code
│   ├── go.mod               # Go module definition
│   ├── go.sum               # Dependency checksums
│   └── bin/                 # Compiled executables
├── assets/                  # Game assets
│   ├── tank/               # Tank sprites
│   ├── mine/               # Mine and explosion effects
│   ├── bonus/              # Gold and bonus items
│   ├── ground/             # Ground textures
│   └── decor/              # Obstacles and decorative elements
├── sounds/                 # Audio files
└── docs/                   # Documentation
```

---

## 3. Game Mechanics

### 3.1 Core Gameplay
- **Perspective:** Top-down 2D
- **Game Area:** 900x400 pixel single screen
- **Game Mode:** Single-player, endless survival
- **Difficulty:** Progressively increasing (mine explosion timers can be shortened)

### 3.2 Controls
| Key | Function |
|-----|----------|
| **Space** | Move tank forward |
| **Left Arrow** | Rotate tank clockwise |
| **Right Arrow** | Rotate tank counterclockwise |

### 3.3 Game Objects

#### 3.3.1 Tank (Player)
- **Starting Position:** Bottom left corner (50, 350)
- **Starting Armor:** 100 HP
- **Movement Speed:** 2.0 pixels/frame
- **Rotation Speed:** 0.05 radians/frame
- **Sprite Composition:** Hull_01.png + Gun_01.png + Track_1_A.png
- **Effect:** Exhaust_Fire.png (when moving)
- **Scale:** 25% (0.25x) of original size

#### 3.3.2 Mines
- **Maximum Count:** 5 simultaneous
- **Explosion Time:** 10-30 seconds (random)
- **Damage Amount:** 10 HP
- **Damage Radius:** 50 pixels
- **Explosion Animation:** 7 frames (Mine_Explosion_A_001 - A_007)
- **Animation Speed:** 8 frames/transition (approximately 1 second total)
- **Respawn:** New mine added immediately after explosion
- **Sprite:** Mine.png
- **Scale:** 25% (0.25x) of original size

#### 3.3.3 Gold Bonuses
- **Maximum Count:** 10 simultaneous
- **Point Value:** 1 point/gold
- **Collection Radius:** 15 pixels
- **Respawn:** New gold added immediately after collection
- **Sprite:** Coin_A.png
- **Scale:** 25% (0.25x) of original size

#### 3.3.4 Obstacles
- **Count:** 4 fixed
- **Type:** Czech hedgehog anti-tank obstacles
- **Collision Radius:** 7.5 pixels
- **Sprite:** Czech_Hdgehog_A.png
- **Scale:** 25% (0.25x) of original size

#### 3.3.5 Ground
- **Texture Variety:** 2 types (Ground_Tile_01_C.png, Ground_Tile_02_C.png)
- **Pattern:** Random mix tiling
- **Scale:** Original size (not scaled)

---

## 4. Game Systems

### 4.1 Collision Detection System
- **Method:** Euclidean distance calculation
- **Optimization:** Single-pass collision detection per frame
- **Collision Radii:**
  - Tank ↔ Obstacle: 7.5 pixels
  - Tank ↔ Bonus: 15 pixels
  - Tank ↔ Mine Damage: 50 pixels
  - Object Overlap Prevention: 12.5 pixels

### 4.2 Animation System
- **Mine Explosions:** Frame-counter based (8 frames delay)
- **Tank Movement:** Real-time directional movement
- **Exhaust Effects:** Position-based particle simulation

### 4.3 Spawn System
- **Object Placement:** Random position generation
- **Overlap Prevention:** Distance-based validation
- **Boundary Checking:** Screen edge constraints

### 4.4 Scoring System
- **Score Calculation:** 1 point = 1 gold
- **Display:** Real-time UI display
- **Game End:** Final score presentation

---

## 5. Advanced Features

### 5.1 Asset Scaling System
- **Global Scale Factor:** 0.25x for all game objects
- **Proportional Scaling:** All objects scaled at the same ratio
- **Collision Adaptation:** Collision radii adjusted for scaled sizes

### 5.2 Direction Correction System
- **Tank Direction:** Visual orientation + 90° counterclockwise movement
- **Correction Formula:** `angle - π/2` for movement calculations
- **Consistent Application:** Movement, collision revert, exhaust positioning

### 5.3 Performance Optimizations
- **Frame Rate Control:** Ebiten default 60 FPS
- **Memory Management:** Pre-loaded assets, efficient sprite rendering
- **Collision Optimization:** Early break conditions, distance-based filtering

---

## 6. Asset Specifications

### 6.1 Required Asset Files

#### Tank Components
- `Hull_01.png` - Tank body
- `Gun_01.png` - Tank gun barrel
- `Track_1_A.png` - Tank tracks
- `Exhaust_Fire.png` - Exhaust fire effect

#### Mine System
- `Mine.png` - Mine sprite
- `Mine_Explosion_A_001.png` through `Mine_Explosion_A_007.png` - Explosion animation

#### Collectibles
- `Coin_A.png` - Gold bonus

#### Environment
- `Ground_Tile_01_C.png` - Ground texture 1
- `Ground_Tile_02_C.png` - Ground texture 2
- `Czech_Hdgehog_A.png` - Obstacle sprite

#### Audio (Optional)
- `MainTitle_Wagner_Walkure.mp3` - Game music

### 6.2 Asset Requirements
- **Format:** PNG (sprites), MP3 (audio)
- **Resolution:** Variable (0.25x scale will be applied)
- **Color Depth:** 32-bit RGBA
- **Compression:** Lossless PNG compression

---

## 7. Game Flow

### 7.1 Initialization Sequence
1. Asset loading and initialization
2. Tank placement in bottom left corner
3. 5 mines placed at random locations
4. 10 gold bonuses placed at random locations
5. 4 obstacles placed at random locations
6. Random tiling of ground textures

### 7.2 Main Game Loop
1. **Input Processing:** Check keyboard inputs
2. **Movement Update:** Update tank position and angle
3. **Collision Detection:** All collision checks
4. **Mine Management:** Explosion timers and animations
5. **Bonus Collection:** Gold collection check
6. **Damage Calculation:** Mine damage calculation
7. **UI Update:** Score and armor display
8. **Render Frame:** Drawing all objects

### 7.3 Game End
1. Armor ≤ 0 check
2. Final score display
3. Game reset and restart

---

## 8. Quality Assurance

### 8.1 Test Criteria
- **Performance Tests:** 60 FPS stability
- **Collision Tests:** Accurate hit detection
- **Animation Tests:** Smooth explosion sequences
- **Audio Tests:** Background music integration
- **UI Tests:** Score and armor display accuracy

### 8.2 Known Issues and Solutions
1. **Asset Sizing:** Original problem - solved with 25% scaling
2. **Tank Direction:** Movement direction correction applied
3. **Explosion Speed:** Frame-counter system implemented for slower animation
4. **Bonus Collection:** Collection radius increased (15 pixels)
5. **Mine Damage:** Explosion-time damage system corrected

---

## 9. Build and Deployment

### 9.1 Build Commands
```bash
# Development build
go build -o bin/walkure main.go

# Production build (Windows)
GOOS=windows GOARCH=amd64 go build -o bin/walkure.exe main.go

# Clean build
go clean
rm -rf bin/*
```

### 9.2 Dependencies
```go
module walkure

go 1.21

require (
    github.com/hajimehoshi/ebiten/v2 v2.7.3
)
```

### 9.3 Minimum System Requirements
- **OS:** Windows 10 64-bit
- **RAM:** 1GB
- **Storage:** 50MB
- **Graphics:** DirectX 11 compatible
- **Input:** Keyboard

---

## 10. Future Developments

### 10.1 Planned Features
- **Difficulty Scaling:** Progressive difficulty increase system
- **Power-ups:** Temporary enhancements
- **Particle Effects:** Advanced visual effects
- **Sound Integration:** Full sound system integration
- **High Score System:** High score leaderboard

### 10.2 Code Refactoring Suggestions
- **Modular Architecture:** Components in separate files
- **State Management:** Game state machine implementation
- **Asset Manager:** Centralized asset loading system
- **Configuration System:** External config file support

---

## 11. Development Notes

### 11.1 AI-Assisted Development Process
This game was developed with Grok Code Fast 1 (Preview) AI assistant. Problems encountered during development and their solutions:

1. **Struct Field Errors:** Mine explosion system refactoring
2. **Asset Scaling Issues:** Global scaling factor implementation
3. **Movement Direction Problems:** Trigonometric angle corrections
4. **Animation Timing Issues:** Frame-counter based animation system
5. **Collision Detection Bugs:** Radius adjustments for scaled assets

### 11.2 Code Quality Standards
- **Naming Conventions:** Go standard naming (camelCase, PascalCase)
- **Error Handling:** Explicit error checking and handling
- **Comments:** Inline documentation for complex logic
- **Performance:** Efficient game loop implementation
- **Maintainability:** Clear structure and logical organization

---

**Document Version:** 2.0  
**Last Updated:** September 4, 2025  
**Development Status:** Completed Core Features  
**Next Review:** Post-release feedback integration
