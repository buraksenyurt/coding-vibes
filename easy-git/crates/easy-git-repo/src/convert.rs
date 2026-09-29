//! Small translations from gix types to core types.

use easy_git_core::{CommitId, Signature, Timestamp};

pub fn commit_id(id: &gix::oid) -> CommitId {
    let mut bytes = [0u8; 20];
    bytes.copy_from_slice(id.as_bytes());
    CommitId::from_bytes(bytes)
}

pub fn object_id(id: CommitId) -> gix::ObjectId {
    gix::ObjectId::from_bytes_or_panic(id.as_bytes())
}

pub fn signature(sig: gix::actor::SignatureRef<'_>) -> Signature {
    let time = sig.time().unwrap_or_default();
    Signature {
        name: sig.name.to_string(),
        email: sig.email.to_string(),
        time: Timestamp {
            seconds: time.seconds,
            offset_minutes: time.offset / 60,
        },
    }
}
