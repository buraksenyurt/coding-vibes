use bevy::prelude::*;
use bevy::app::AppExit;
use crate::states::GameState;

// Input handling for menu navigation
pub fn handle_menu_input(
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

// Handle escape key for going back to menu
pub fn handle_escape_input(
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
