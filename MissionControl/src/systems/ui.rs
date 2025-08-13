use bevy::prelude::*;
use crate::{components::*, resources::*, constants::*, systems::persistence::save_scores};

// Menu screen setup
pub fn setup_menu(mut commands: Commands) {
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

pub fn cleanup_menu(mut commands: Commands, menu_query: Query<Entity, With<MenuRoot>>) {
    for entity in menu_query.iter() {
        commands.entity(entity).despawn();
    }
}

// Story screen setup
pub fn setup_story(mut commands: Commands) {
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

pub fn cleanup_story(mut commands: Commands, story_query: Query<Entity, With<StoryRoot>>) {
    for entity in story_query.iter() {
        commands.entity(entity).despawn();
    }
}

// Scores screen setup
pub fn setup_scores(mut commands: Commands, scoreboard: Res<ScoreBoard>) {
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

pub fn cleanup_scores(mut commands: Commands, scores_query: Query<Entity, With<ScoresRoot>>) {
    for entity in scores_query.iter() {
        commands.entity(entity).despawn();
    }
}

// Game over screen setup
pub fn setup_game_over(mut commands: Commands, scoreboard: ResMut<ScoreBoard>, counters: Res<Counters>) {
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

pub fn cleanup_game_over(mut commands: Commands, game_over_query: Query<Entity, With<GameOverRoot>>) {
    for entity in game_over_query.iter() {
        commands.entity(entity).despawn();
    }
}

// HUD update system
pub fn update_hud(
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
