// Game constants
pub const SCREEN_W: f32 = 500.0;
pub const SCREEN_H: f32 = 650.0;
pub const HUD_H: f32 = 50.0;
pub const SCENE_H: f32 = 600.0; // SCREEN_H - HUD_H

pub const GAME_SECONDS: f32 = 180.0; // 3 minutes

// Movement speeds (px/sec)
pub const TANK_SPEED: f32 = 120.0;
pub const DONUT_SPEED: f32 = 360.0;
pub const INVADER_SPEED_SLOW: f32 = 60.0;
pub const INVADER_SPEED_MED: f32 = 80.0;
pub const INVADER_SPEED_FAST: f32 = 120.0;
pub const SPATULA_FALL_SPEED: f32 = 120.0;

// Fire rates
pub const FIRE_COOLDOWN_NORMAL: f32 = 1.0; // 1 shot/sec
pub const FIRE_COOLDOWN_RAPID: f32 = 0.25; // 250ms

// Limits
pub const MAX_INVADERS: usize = 5;
pub const MAX_LOVERS_TO_LOSE: i32 = 100;
pub const MAX_LANDED_SHIPS_TO_LOSE: i32 = 10;

// Effects
pub const RAPID_FIRE_DURATION: f32 = 10.0; // seconds
pub const BOMB_DURATION: f32 = 10.0; // seconds
pub const POWERUP_MIN_GAP: f32 = 15.0; // seconds

// Scoring
pub const START_SCORE: i32 = 100;
pub const SCORE_ON_SHIP_DOWN: i32 = 10;

// Data file
pub const SCORE_FILE: &str = "game.dat";
