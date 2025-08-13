use bevy::prelude::*;
use mission_control::*;

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
