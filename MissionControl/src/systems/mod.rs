// Systems module organization
pub mod game;
pub mod ui;
pub mod input;
pub mod persistence;
pub mod setup;

// Re-export all systems
pub use game::*;
pub use ui::*;
pub use input::*;
pub use persistence::*;
pub use setup::*;
