use bevy::prelude::*;

// Game entities
#[derive(Component, Default)]
pub struct Tank;

#[derive(Component)]
pub struct Donut;

#[derive(Component, Copy, Clone)]
pub enum InvaderSize { 
    Small, 
    Midi, 
    Large 
}

#[derive(Component)]
pub struct Invader { 
    pub size: InvaderSize, 
    pub speed: f32, 
    pub troop_count: i32, 
    pub health: i32 
}

#[derive(Component)]
pub struct Spatula;

#[derive(Component)]
pub struct DespawnOnExit;

// UI marker components
#[derive(Component)]
pub struct HudRoot;

#[derive(Component)]
pub struct HudText;

#[derive(Component)]
pub struct MenuRoot;

#[derive(Component)]
pub struct GameOverRoot;

#[derive(Component)]
pub struct StoryRoot;

#[derive(Component)]
pub struct ScoresRoot;
