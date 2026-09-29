use std::fmt;
use std::str::FromStr;

/// A SHA-1 object id. Twenty raw bytes, not a hex string: cheaper to hash,
/// compare and copy, and impossible to hold in a malformed state.
#[derive(Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct CommitId([u8; 20]);

impl CommitId {
    pub const fn from_bytes(bytes: [u8; 20]) -> Self {
        Self(bytes)
    }

    pub const fn as_bytes(&self) -> &[u8; 20] {
        &self.0
    }

    /// Full 40 character lowercase hex.
    pub fn to_hex(&self) -> String {
        self.to_string()
    }

    /// The familiar 7 character abbreviation.
    pub fn short(&self) -> String {
        let mut hex = self.to_hex();
        hex.truncate(7);
        hex
    }
}

impl fmt::Display for CommitId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        for byte in self.0 {
            write!(f, "{byte:02x}")?;
        }
        Ok(())
    }
}

impl fmt::Debug for CommitId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "CommitId({})", self.short())
    }
}

/// Returned when a string is not a 40 character hex id.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ParseCommitIdError(pub String);

impl fmt::Display for ParseCommitIdError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "'{}' is not a 40 character hex commit id", self.0)
    }
}

impl std::error::Error for ParseCommitIdError {}

impl FromStr for CommitId {
    type Err = ParseCommitIdError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let err = || ParseCommitIdError(s.to_owned());
        if s.len() != 40 {
            return Err(err());
        }
        let mut bytes = [0u8; 20];
        for (i, chunk) in s.as_bytes().chunks(2).enumerate() {
            let pair = std::str::from_utf8(chunk).map_err(|_| err())?;
            bytes[i] = u8::from_str_radix(pair, 16).map_err(|_| err())?;
        }
        Ok(Self(bytes))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trips_through_hex() {
        let hex = "a3f9c21b7e0d4c5a8f1e2d3c4b5a69788796a5b4";
        let id: CommitId = hex.parse().unwrap();
        assert_eq!(id.to_hex(), hex);
        assert_eq!(id.short(), "a3f9c21");
    }

    #[test]
    fn rejects_bad_input() {
        assert!("abc".parse::<CommitId>().is_err());
        assert!("zz".repeat(20).parse::<CommitId>().is_err());
    }
}
