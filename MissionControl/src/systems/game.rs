use bevy::prelude::*;
use rand::Rng;
use crate::{components::*, resources::*, constants::*, states::GameState};

// Tank movement system
pub fn tank_movement(
    mut tank_query: Query<&mut Transform, With<Tank>>,
    keyboard: Res<ButtonInput<KeyCode>>,
    time: Res<Time>,
) {
    if let Ok(mut transform) = tank_query.single_mut() {
        let mut direction = 0.0;
        
        // GDG'ye göre kontroller ters çalışır
        if keyboard.pressed(KeyCode::ArrowLeft) || keyboard.pressed(KeyCode::KeyA) {
            direction = 1.0; // Sağa git
        }
        if keyboard.pressed(KeyCode::ArrowRight) || keyboard.pressed(KeyCode::KeyD) {
            direction = -1.0; // Sola git
        }
        
        let movement = direction * TANK_SPEED * time.delta_secs();
        transform.translation.x += movement;
        
        // Screen bounds
        let half_screen = SCREEN_W / 2.0;
        transform.translation.x = transform.translation.x.clamp(-half_screen + 20.0, half_screen - 20.0);
    }
}

// Tank firing system
pub fn tank_firing(
    mut commands: Commands,
    tank_query: Query<&Transform, With<Tank>>,
    keyboard: Res<ButtonInput<KeyCode>>,
    mut timers: ResMut<Timers>,
    flags: Res<Flags>,
    time: Res<Time>,
) {
    if let Ok(tank_transform) = tank_query.single() {
        // Update fire cooldown
        timers.fire_cooldown.tick(time.delta());
        
        // Fire with different keys, but Space is reserved for bomb
        if (keyboard.pressed(KeyCode::ArrowUp) || keyboard.pressed(KeyCode::KeyW)) 
            && timers.fire_cooldown.finished() {
            
            // Spawn donut
            commands.spawn((
                Sprite {
                    color: Color::srgb(0.8, 0.6, 0.2),
                    custom_size: Some(Vec2::new(8.0, 8.0)),
                    ..default()
                },
                Transform::from_xyz(tank_transform.translation.x, tank_transform.translation.y + 25.0, 0.0),
                Donut,
                DespawnOnExit,
            ));
            
            // Set cooldown based on rapid fire status
            let cooldown = if flags.rapid_fire_active {
                FIRE_COOLDOWN_RAPID
            } else {
                FIRE_COOLDOWN_NORMAL
            };
            timers.fire_cooldown = Timer::from_seconds(cooldown, TimerMode::Once);
        }
    }
}

// Donut movement system
pub fn donut_movement(
    mut donut_query: Query<&mut Transform, With<Donut>>,
    time: Res<Time>,
) {
    for mut transform in donut_query.iter_mut() {
        transform.translation.y += DONUT_SPEED * time.delta_secs();
    }
}

// Invader spawning system
pub fn invader_spawning(
    mut commands: Commands,
    mut timers: ResMut<Timers>,
    invader_query: Query<&Invader>,
    _flags: Res<Flags>,
    time: Res<Time>,
) {
    timers.spawn.tick(time.delta());
    
    if timers.spawn.finished() && invader_query.iter().count() < MAX_INVADERS {
        let mut rng = rand::thread_rng();
        
        let size = match rng.gen_range(0..3) {
            0 => InvaderSize::Small,
            1 => InvaderSize::Midi,
            _ => InvaderSize::Large,
        };
        
        let (width, height, speed) = match size {
            InvaderSize::Small => (20.0, 15.0, INVADER_SPEED_FAST),
            InvaderSize::Midi => (30.0, 20.0, INVADER_SPEED_MED),
            InvaderSize::Large => (50.0, 25.0, INVADER_SPEED_SLOW),
        };
        
        let x_pos = rng.gen_range(-SCREEN_W/2.0 + width/2.0..SCREEN_W/2.0 - width/2.0);
        let troop_count = if rng.gen_bool(0.5) { 5 } else { 10 };
        
        commands.spawn((
            Sprite {
                color: Color::srgb(0.8, 0.2, 0.2),
                custom_size: Some(Vec2::new(width, height)),
                ..default()
            },
            Transform::from_xyz(x_pos, SCENE_H/2.0, 0.0),
            Invader {
                size,
                speed,
                troop_count,
                health: 3,
            },
            DespawnOnExit,
        ));
    }
}

// Invader movement system
pub fn invader_movement(
    mut invader_query: Query<(&mut Transform, &Invader)>,
    flags: Res<Flags>,
    time: Res<Time>,
) {
    // Don't move during bomb effect
    if flags.bomb_active {
        return;
    }
    
    for (mut transform, invader) in invader_query.iter_mut() {
        transform.translation.y -= invader.speed * time.delta_secs();
    }
}

// Collision detection
pub fn collision_system(
    mut commands: Commands,
    donut_query: Query<(Entity, &Transform), (With<Donut>, Without<Invader>)>,
    mut invader_query: Query<(Entity, &Transform, &mut Invader)>,
    mut scoreboard: ResMut<ScoreBoard>,
) {
    for (donut_entity, donut_transform) in donut_query.iter() {
        for (invader_entity, invader_transform, mut invader) in invader_query.iter_mut() {
            let distance = donut_transform.translation.distance(invader_transform.translation);
            
            if distance < 20.0 {
                // Hit!
                commands.entity(donut_entity).despawn();
                invader.health -= 1;
                
                if invader.health <= 0 {
                    commands.entity(invader_entity).despawn();
                    scoreboard.current += SCORE_ON_SHIP_DOWN;
                }
                break;
            }
        }
    }
}

// Invader landing system
pub fn invader_landing(
    mut commands: Commands,
    invader_query: Query<(Entity, &Transform, &Invader)>,
    mut counters: ResMut<Counters>,
    mut scoreboard: ResMut<ScoreBoard>,
) {
    for (entity, transform, invader) in invader_query.iter() {
        if transform.translation.y < -SCENE_H/2.0 {
            // Invader reached ground
            commands.entity(entity).despawn();
            counters.landed_lovers += invader.troop_count;
            counters.landed_ships += 1;
            scoreboard.current = (scoreboard.current - invader.troop_count).max(0);
        }
    }
}

// Spatula spawning
pub fn spatula_spawning(
    mut commands: Commands,
    mut timers: ResMut<Timers>,
    spatula_query: Query<&Spatula>,
    time: Res<Time>,
) {
    timers.powerup_spawn.tick(time.delta());
    
    if timers.powerup_spawn.finished() && spatula_query.iter().count() == 0 {
        let mut rng = rand::thread_rng();
        let x_pos = rng.gen_range(-SCREEN_W/2.0 + 20.0..SCREEN_W/2.0 - 20.0);
        
        commands.spawn((
            Sprite {
                color: Color::srgb(0.2, 0.8, 0.2),
                custom_size: Some(Vec2::new(25.0, 25.0)),
                ..default()
            },
            Transform::from_xyz(x_pos, SCENE_H/2.0, 0.0),
            Spatula,
            DespawnOnExit,
        ));
        
        timers.powerup_spawn = Timer::from_seconds(POWERUP_MIN_GAP, TimerMode::Once);
    }
}

// Spatula movement and collection
pub fn spatula_system(
    mut commands: Commands,
    mut spatula_query: Query<(Entity, &mut Transform), With<Spatula>>,
    tank_query: Query<&Transform, (With<Tank>, Without<Spatula>)>,
    mut timers: ResMut<Timers>,
    mut flags: ResMut<Flags>,
    time: Res<Time>,
) {
    if let Ok(tank_transform) = tank_query.single() {
        for (spatula_entity, mut spatula_transform) in spatula_query.iter_mut() {
            // Move spatula down
            spatula_transform.translation.y -= SPATULA_FALL_SPEED * time.delta_secs();
            
            // Check collection
            let distance = spatula_transform.translation.distance(tank_transform.translation);
            if distance < 25.0 {
                commands.entity(spatula_entity).despawn();
                flags.rapid_fire_active = true;
                timers.rapid_fire = Timer::from_seconds(RAPID_FIRE_DURATION, TimerMode::Once);
            }
            
            // Despawn if out of bounds
            if spatula_transform.translation.y < -SCENE_H/2.0 {
                commands.entity(spatula_entity).despawn();
            }
        }
    }
}

// Rapid fire system
pub fn rapid_fire_system(
    mut timers: ResMut<Timers>,
    mut flags: ResMut<Flags>,
    time: Res<Time>,
) {
    if flags.rapid_fire_active {
        timers.rapid_fire.tick(time.delta());
        if timers.rapid_fire.finished() {
            flags.rapid_fire_active = false;
        }
    }
}

// Bomb system
pub fn bomb_system(
    keyboard: Res<ButtonInput<KeyCode>>,
    mut flags: ResMut<Flags>,
    mut timers: ResMut<Timers>,
    time: Res<Time>,
) {
    if keyboard.just_pressed(KeyCode::Space) && !flags.bomb_used && !flags.bomb_active {
        flags.bomb_used = true;
        flags.bomb_active = true;
        timers.bomb = Timer::from_seconds(BOMB_DURATION, TimerMode::Once);
    }
    
    if flags.bomb_active {
        timers.bomb.tick(time.delta());
        if timers.bomb.finished() {
            flags.bomb_active = false;
        }
    }
}

// Cleanup system for entities going off-screen
pub fn cleanup_offscreen(
    mut commands: Commands,
    query: Query<(Entity, &Transform), With<DespawnOnExit>>,
) {
    for (entity, transform) in query.iter() {
        if transform.translation.y > SCREEN_H/2.0 + 50.0 || 
           transform.translation.y < -SCREEN_H/2.0 - 50.0 {
            commands.entity(entity).despawn();
        }
    }
}

// Game timer and state transitions
pub fn game_timer(
    mut timers: ResMut<Timers>,
    counters: Res<Counters>,
    mut next_state: ResMut<NextState<GameState>>,
    time: Res<Time>,
) {
    timers.game.tick(time.delta());
    
    // Check win/lose conditions
    if counters.landed_lovers >= MAX_LOVERS_TO_LOSE || 
       counters.landed_ships >= MAX_LANDED_SHIPS_TO_LOSE {
        next_state.set(GameState::GameOver);
    } else if timers.game.finished() {
        next_state.set(GameState::GameOver);
    }
}

// Cleanup game entities
pub fn cleanup_game(
    mut commands: Commands,
    entities_query: Query<Entity, With<DespawnOnExit>>,
    tank_query: Query<Entity, With<Tank>>,
    hud_query: Query<Entity, With<HudRoot>>,
) {
    for entity in entities_query.iter() {
        commands.entity(entity).despawn();
    }
    for entity in tank_query.iter() {
        commands.entity(entity).despawn();
    }
    for entity in hud_query.iter() {
        commands.entity(entity).despawn();
    }
}
