use bevy::prelude::*;
use bevy::app::AppExit;
use rand::Rng;
use serde::{Deserialize, Serialize};
use std::fs;

// ==========================================
// Constants from GDD
// ==========================================
const SCREEN_W: f32 = 500.0;
const SCREEN_H: f32 = 650.0;
const HUD_H: f32 = 50.0;
const SCENE_H: f32 = 600.0; // SCREEN_H - HUD_H

const GAME_SECONDS: f32 = 180.0; // 3 minutes

// Movement speeds (px/sec)
const TANK_SPEED: f32 = 120.0;
const DONUT_SPEED: f32 = 360.0;
const INVADER_SPEED_SLOW: f32 = 60.0;
const INVADER_SPEED_MED: f32 = 80.0;
const INVADER_SPEED_FAST: f32 = 120.0;
const SPATULA_FALL_SPEED: f32 = 120.0;

// Fire rates
const FIRE_COOLDOWN_NORMAL: f32 = 1.0; // 1 shot/sec
const FIRE_COOLDOWN_RAPID: f32 = 0.25; // 250ms

// Limits
const MAX_INVADERS: usize = 5;
const MAX_LOVERS_TO_LOSE: i32 = 100;
const MAX_LANDED_SHIPS_TO_LOSE: i32 = 10;

// Effects
const RAPID_FIRE_DURATION: f32 = 10.0; // seconds
const BOMB_DURATION: f32 = 10.0; // seconds
const POWERUP_MIN_GAP: f32 = 15.0; // seconds

// Scoring
const START_SCORE: i32 = 100;
const SCORE_ON_SHIP_DOWN: i32 = 10;

// Data file
const SCORE_FILE: &str = "game.dat";

// ==========================================
// App State
// ==========================================
#[derive(States, Debug, Hash, Eq, PartialEq, Clone, Default)]
enum GameState {
    #[default]
    MainMenu,
    Story,
    Game,
    GameOver,
    Scores,
}

// ==========================================
// Components
// ==========================================
#[derive(Component, Default)]
struct Tank;

#[derive(Component)]
struct Donut;

#[derive(Component, Copy, Clone)]
enum InvaderSize { Small, Midi, Large }

#[derive(Component)]
struct Invader { 
    size: InvaderSize, 
    speed: f32, 
    troop_count: i32, 
    health: i32 
}

#[derive(Component)]
struct Spatula;

#[derive(Component)]
struct DespawnOnExit;

// Tags for UI nodes
#[derive(Component)]
struct HudRoot;

#[derive(Component)]
struct HudText;

#[derive(Component)]
struct MenuRoot;

#[derive(Component)]
struct GameOverRoot;

#[derive(Component)]
struct StoryRoot;

#[derive(Component)]
struct ScoresRoot;

// ==========================================
// Resources
// ==========================================
#[derive(Resource, Default)]
struct ScoreBoard {
    current: i32,
    best: i32,
    history: Vec<i32>, // last 5
}

#[derive(Resource, Default)]
struct Counters {
    landed_lovers: i32,
    landed_ships: i32,
}

#[derive(Resource, Default)]
struct Timers {
    spawn: Timer,
    powerup_spawn: Timer,
    game: Timer,
    fire_cooldown: Timer,
    rapid_fire: Timer,
    bomb: Timer,
}

#[derive(Resource, Default)]
struct Flags {
    rapid_fire_active: bool,
    bomb_used: bool,
    bomb_active: bool,
}

// ==========================================
// Persistence
// ==========================================
#[derive(Serialize, Deserialize, Default)]
struct ScoreData { 
    best: i32, 
    history: Vec<i32> 
}

fn load_scores(mut scoreboard: ResMut<ScoreBoard>) {
    if let Ok(contents) = fs::read_to_string(SCORE_FILE) {
        if let Ok(data) = serde_json::from_str::<ScoreData>(&contents) {
            scoreboard.best = data.best;
            scoreboard.history = data.history;
        }
    }
}

fn save_scores(scoreboard: &ScoreBoard) {
    let mut hist = scoreboard.history.clone();
    hist.push(scoreboard.current);
    if hist.len() > 5 { 
        hist = hist[hist.len()-5..].to_vec(); 
    }
    let best = hist.iter().cloned()
        .chain(std::iter::once(scoreboard.best))
        .max().unwrap_or(0)
        .max(scoreboard.current);
    let data = ScoreData { best, history: hist };
    if let Ok(json) = serde_json::to_string_pretty(&data) {
        let _ = fs::write(SCORE_FILE, json);
    }
}

// ==========================================
// Setup
// ==========================================
fn setup_camera(mut commands: Commands) { 
    commands.spawn(Camera2d::default()); 
}

fn setup_hud(mut commands: Commands) {
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

fn setup_game(
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

// ==========================================
// Game Systems
// ==========================================

// Menu system
fn setup_menu(mut commands: Commands) {
    commands
        .spawn((
            Node {
                width: Val::Percent(100.0),
                height: Val::Percent(100.0),
                align_items: AlignItems::Center,
                justify_content: JustifyContent::Center,
                flex_direction: FlexDirection::Column,
                ..default()
            },
            BackgroundColor(Color::srgb(0.0, 0.0, 0.1)),
            MenuRoot,
        ))
        .with_children(|parent| {
            // Title
            parent.spawn((
                Text::new("MISSION CONTROL"),
                TextColor(Color::WHITE),
                Node {
                    margin: UiRect::all(Val::Px(20.0)),
                    ..default()
                },
            ));
            
            // Menu options
            parent.spawn((
                Text::new("ENTER - Start Game\nS - Story\nC - Scores\nESC - Exit"),
                TextColor(Color::srgb(0.8, 0.8, 0.8)),
            ));
        });
}

fn cleanup_menu(mut commands: Commands, menu_query: Query<Entity, With<MenuRoot>>) {
    for entity in menu_query.iter() {
        commands.entity(entity).despawn();
    }
}

// Tank movement system
fn tank_movement(
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
fn tank_firing(
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
fn donut_movement(
    mut donut_query: Query<&mut Transform, With<Donut>>,
    time: Res<Time>,
) {
    for mut transform in donut_query.iter_mut() {
        transform.translation.y += DONUT_SPEED * time.delta_secs();
    }
}

// Invader spawning system
fn invader_spawning(
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
fn invader_movement(
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
fn collision_system(
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
fn invader_landing(
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
fn spatula_spawning(
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
fn spatula_system(
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
fn rapid_fire_system(
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
fn bomb_system(
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
fn cleanup_offscreen(
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

// HUD update system
fn update_hud(
    mut text_query: Query<&mut Text, With<HudText>>,
    scoreboard: Res<ScoreBoard>,
    counters: Res<Counters>,
    timers: Res<Timers>,
    flags: Res<Flags>,
) {
    if let Ok(mut text) = text_query.single_mut() {
        let remaining_time = (GAME_SECONDS - timers.game.elapsed_secs()).max(0.0) as i32;
        let bomb_status = if flags.bomb_used { "Used" } else { "Ready" };
        let rapid_remaining = if flags.rapid_fire_active {
            (RAPID_FIRE_DURATION - timers.rapid_fire.elapsed_secs()).max(0.0) as i32
        } else { 0 };
        
        text.0 = format!(
            "Score: {} | Lovers: {} | Time: {}s | Bomb: {} | Rapid: {}s",
            scoreboard.current, counters.landed_lovers, remaining_time, bomb_status, rapid_remaining
        );
    }
}

// Game timer and state transitions
fn game_timer(
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

// Input handling for state transitions
fn handle_menu_input(
    keyboard: Res<ButtonInput<KeyCode>>,
    mut next_state: ResMut<NextState<GameState>>,
    mut app_exit: EventWriter<AppExit>,
) {
    if keyboard.just_pressed(KeyCode::Enter) {
        next_state.set(GameState::Game);
    } else if keyboard.just_pressed(KeyCode::KeyS) {
        next_state.set(GameState::Story);
    } else if keyboard.just_pressed(KeyCode::KeyC) {
        next_state.set(GameState::Scores);
    } else if keyboard.just_pressed(KeyCode::Escape) {
        app_exit.write(AppExit::Success);
    }
}

// Story screen setup
fn setup_story(mut commands: Commands) {
    commands
        .spawn((
            Node {
                width: Val::Percent(100.0),
                height: Val::Percent(100.0),
                align_items: AlignItems::Center,
                justify_content: JustifyContent::Center,
                flex_direction: FlexDirection::Column,
                padding: UiRect::all(Val::Px(20.0)),
                ..default()
            },
            BackgroundColor(Color::srgb(0.0, 0.0, 0.1)),
            StoryRoot,
        ))
        .with_children(|parent| {
            parent.spawn((
                Text::new("MISSION CONTROL - THE STORY"),
                TextColor(Color::WHITE),
                Node {
                    margin: UiRect::bottom(Val::Px(20.0)),
                    ..default()
                },
            ));
            
            parent.spawn((
                Text::new(
                    "Earth's future depends on this final line of defense.\n\n\
                    Captain Gadot has taken position behind the Silicon-Enhanced Donut Gun\n\
                    to destroy approaching alien ships. The aliens' greatest weakness is their\n\
                    love for Earth's sweet donuts - after eating 3 donuts in a row, they\n\
                    swell up and explode, taking their invasion ships down with them.\n\n\
                    Time is running out. The invasion lasts only 3 minutes.\n\
                    Protect Earth at all costs!\n\n\
                    ESC - Back to Menu"
                ),
                TextColor(Color::srgb(0.8, 0.8, 0.8)),
            ));
        });
}

fn cleanup_story(mut commands: Commands, story_query: Query<Entity, With<StoryRoot>>) {
    for entity in story_query.iter() {
        commands.entity(entity).despawn();
    }
}

// Scores screen setup
fn setup_scores(mut commands: Commands, scoreboard: Res<ScoreBoard>) {
    commands
        .spawn((
            Node {
                width: Val::Percent(100.0),
                height: Val::Percent(100.0),
                align_items: AlignItems::Center,
                justify_content: JustifyContent::Center,
                flex_direction: FlexDirection::Column,
                ..default()
            },
            BackgroundColor(Color::srgb(0.0, 0.0, 0.1)),
            ScoresRoot,
        ))
        .with_children(|parent| {
            parent.spawn((
                Text::new("HIGH SCORES"),
                TextColor(Color::WHITE),
                Node {
                    margin: UiRect::bottom(Val::Px(20.0)),
                    ..default()
                },
            ));
            
            parent.spawn((
                Text::new(format!("Best Score: {}", scoreboard.best)),
                TextColor(Color::srgb(1.0, 1.0, 0.0)),
                Node {
                    margin: UiRect::bottom(Val::Px(10.0)),
                    ..default()
                },
            ));
            
            let history_text = if scoreboard.history.is_empty() {
                "No games played yet".to_string()
            } else {
                format!("Recent Scores:\n{}", 
                    scoreboard.history.iter()
                        .enumerate()
                        .map(|(i, score)| format!("{}. {}", i + 1, score))
                        .collect::<Vec<_>>()
                        .join("\n")
                )
            };
            
            parent.spawn((
                Text::new(history_text),
                TextColor(Color::srgb(0.8, 0.8, 0.8)),
                Node {
                    margin: UiRect::bottom(Val::Px(20.0)),
                    ..default()
                },
            ));
            
            parent.spawn((
                Text::new("ESC - Back to Menu"),
                TextColor(Color::srgb(0.6, 0.6, 0.6)),
            ));
        });
}

fn cleanup_scores(mut commands: Commands, scores_query: Query<Entity, With<ScoresRoot>>) {
    for entity in scores_query.iter() {
        commands.entity(entity).despawn();
    }
}

// Game over screen setup
fn setup_game_over(mut commands: Commands, scoreboard: ResMut<ScoreBoard>, counters: Res<Counters>) {
    // Save scores when game ends
    save_scores(&*scoreboard);
    
    let result = if counters.landed_lovers >= MAX_LOVERS_TO_LOSE {
        "DEFEAT - Too many Donut Lovers reached Earth!"
    } else if counters.landed_ships >= MAX_LANDED_SHIPS_TO_LOSE {
        "DEFEAT - Too many ships landed!"
    } else {
        "TIME'S UP!"
    };
    
    commands
        .spawn((
            Node {
                width: Val::Percent(100.0),
                height: Val::Percent(100.0),
                align_items: AlignItems::Center,
                justify_content: JustifyContent::Center,
                flex_direction: FlexDirection::Column,
                ..default()
            },
            BackgroundColor(Color::srgb(0.1, 0.0, 0.0)),
            GameOverRoot,
        ))
        .with_children(|parent| {
            parent.spawn((
                Text::new("GAME OVER"),
                TextColor(Color::WHITE),
                Node {
                    margin: UiRect::bottom(Val::Px(20.0)),
                    ..default()
                },
            ));
            
            parent.spawn((
                Text::new(result),
                TextColor(Color::srgb(1.0, 0.5, 0.5)),
                Node {
                    margin: UiRect::bottom(Val::Px(20.0)),
                    ..default()
                },
            ));
            
            parent.spawn((
                Text::new(format!(
                    "Final Score: {}\nDonut Lovers Landed: {}\nShips Landed: {}",
                    scoreboard.current, counters.landed_lovers, counters.landed_ships
                )),
                TextColor(Color::srgb(0.8, 0.8, 0.8)),
                Node {
                    margin: UiRect::bottom(Val::Px(20.0)),
                    ..default()
                },
            ));
            
            parent.spawn((
                Text::new("ESC - Back to Menu"),
                TextColor(Color::srgb(0.6, 0.6, 0.6)),
            ));
        });
}

fn cleanup_game_over(mut commands: Commands, game_over_query: Query<Entity, With<GameOverRoot>>) {
    for entity in game_over_query.iter() {
        commands.entity(entity).despawn();
    }
}

fn cleanup_game(
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

fn handle_escape_input(
    keyboard: Res<ButtonInput<KeyCode>>,
    mut next_state: ResMut<NextState<GameState>>,
    state: Res<State<GameState>>,
) {
    if keyboard.just_pressed(KeyCode::Escape) {
        match state.get() {
            GameState::Story | GameState::Scores | GameState::GameOver => {
                next_state.set(GameState::MainMenu);
            },
            _ => {}
        }
    }
}

fn main() {
    App::new()
        .add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: Some(Window {
                title: "Mission Control".into(),
                resolution: (SCREEN_W, SCREEN_H).into(),
                resizable: false,
                ..default()
            }),
            ..default()
        }))
        .insert_state(GameState::MainMenu)
        .init_resource::<ScoreBoard>()
        .init_resource::<Counters>()
        .insert_resource(Timers::default())
        .insert_resource(Flags::default())
        .add_systems(Startup, (setup_camera, load_scores))
        
        // Menu systems
        .add_systems(OnEnter(GameState::MainMenu), setup_menu)
        .add_systems(OnExit(GameState::MainMenu), cleanup_menu)
        .add_systems(Update, handle_menu_input.run_if(in_state(GameState::MainMenu)))
        
        // Story systems
        .add_systems(OnEnter(GameState::Story), setup_story)
        .add_systems(OnExit(GameState::Story), cleanup_story)
        
        // Scores systems
        .add_systems(OnEnter(GameState::Scores), setup_scores)
        .add_systems(OnExit(GameState::Scores), cleanup_scores)
        
        // Game systems
        .add_systems(OnEnter(GameState::Game), (setup_game, setup_hud))
        .add_systems(OnExit(GameState::Game), cleanup_game)
        .add_systems(
            Update,
            (
                tank_movement,
                tank_firing,
                donut_movement,
                invader_spawning,
                invader_movement,
                collision_system,
                invader_landing,
                spatula_spawning,
                spatula_system,
                rapid_fire_system,
                bomb_system,
                cleanup_offscreen,
                update_hud,
                game_timer,
            ).run_if(in_state(GameState::Game)),
        )
        
        // Game over systems
        .add_systems(OnEnter(GameState::GameOver), setup_game_over)
        .add_systems(OnExit(GameState::GameOver), cleanup_game_over)
        
        // Global systems
        .add_systems(Update, handle_escape_input)
        .run();
}
