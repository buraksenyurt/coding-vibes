use bevy::prelude::*;

// App State
#[derive(States, Debug, Hash, Eq, PartialEq, Clone, Default)]
pub enum GameState {
    #[default]
    MainMenu,
    Story,
    Game,
    GameOver,
    Scores,
}
