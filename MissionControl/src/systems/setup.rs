use bevy::prelude::*;
use crate::{components::*, resources::*, constants::*};

// Setup systems
pub fn setup_camera(mut commands: Commands) { 
    commands.spawn(Camera2d::default()); 
}

pub fn setup_hud(mut commands: Commands) {
    commands
        .spawn((
            Node {
                width: Val::Px(SCREEN_W),
                height: Val::Px(HUD_H),
                ..default()
            },
            BackgroundColor(Color::srgba(0.1, 0.1, 0.1, 0.8)),
            HudRoot
        ))
        .with_children(|p| {
            p.spawn((
                Text::new("Score: 0 | Lovers: 0 | Time: 180 | Bomb: Ready | Rapid: 0s"),
                TextColor(Color::WHITE),
                HudText
            ));
        });
}

pub fn setup_game(
    mut commands: Commands, 
    mut scoreboard: ResMut<ScoreBoard>, 
    mut counters: ResMut<Counters>, 
    mut timers: ResMut<Timers>, 
    mut flags: ResMut<Flags>
) {
    // Reset state
    scoreboard.current = START_SCORE;
    counters.landed_lovers = 0;
    counters.landed_ships = 0;

    timers.spawn = Timer::from_seconds(1.5, TimerMode::Repeating);
    timers.powerup_spawn = Timer::from_seconds(POWERUP_MIN_GAP, TimerMode::Repeating);
    timers.game = Timer::from_seconds(GAME_SECONDS, TimerMode::Once);
    timers.fire_cooldown = Timer::from_seconds(0.0, TimerMode::Once);
    timers.rapid_fire = Timer::from_seconds(0.0, TimerMode::Once);
    timers.bomb = Timer::from_seconds(0.0, TimerMode::Once);

    flags.rapid_fire_active = false;
    flags.bomb_used = false;
    flags.bomb_active = false;

    // Tank spawn at bottom center
    commands.spawn((
        Sprite {
            color: Color::srgb(0.2, 0.8, 0.9),
            custom_size: Some(Vec2::new(40.0, 20.0)),
            ..default()
        },
        Transform::from_xyz(0.0, -SCENE_H/2.0 + 30.0, 0.0),
        Tank,
    ));
}
