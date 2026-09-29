//! Creates the sample "metro line story" repository for manual testing.
//!
//! cargo run -p easy-git-repo --example make-sample-repo -- C:\temp\easy-git-sample

fn main() {
    let Some(root) = std::env::args().nth(1) else {
        eprintln!("usage: make-sample-repo <empty folder>");
        std::process::exit(2);
    };
    match easy_git_repo::fixture::build_sample_repo(root.as_ref()) {
        Ok(sample) => println!("sample repository ready: {}", sample.work_dir.display()),
        Err(err) => {
            eprintln!("{err}");
            std::process::exit(1);
        }
    }
}
