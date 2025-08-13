use bevy::prelude::*;
use serde::{Deserialize, Serialize};

// Game resources
#[derive(Resource, Default)]
pub struct ScoreBoard {
    pub current: i32,
    pub best: i32,
    pub history: Vec<i32>, // last 5
}

#[derive(Resource, Default)]
pub struct Counters {
    pub landed_lovers: i32,
    pub landed_ships: i32,
}

#[derive(Resource, Default)]
pub struct Timers {
    pub spawn: Timer,
    pub powerup_spawn: Timer,
    pub game: Timer,
    pub fire_cooldown: Timer,
    pub rapid_fire: Timer,
    pub bomb: Timer,
}

#[derive(Resource, Default)]
pub struct Flags {
    pub rapid_fire_active: bool,
    pub bomb_used: bool,
    pub bomb_active: bool,
}

// Persistence data structures
#[derive(Serialize, Deserialize, Default)]
pub struct ScoreData { 
    pub best: i32, 
    pub history: Vec<i32> 
}
