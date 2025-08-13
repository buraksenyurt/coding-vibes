use bevy::prelude::*;
use std::fs;
use crate::{resources::*, constants::SCORE_FILE};

// Score persistence functions
pub fn load_scores(mut scoreboard: ResMut<ScoreBoard>) {
    if let Ok(contents) = fs::read_to_string(SCORE_FILE) {
        if let Ok(data) = serde_json::from_str::<ScoreData>(&contents) {
            scoreboard.best = data.best;
            scoreboard.history = data.history;
        }
    }
}

pub fn save_scores(scoreboard: &ScoreBoard) {
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
