use base64::prelude::{BASE64_URL_SAFE_NO_PAD, Engine as _};
use ring::{aead, digest, rand as ring_rand};

pub(super) fn random_token() -> anyhow::Result<String> {
    use ring_rand::SecureRandom as _;
    let mut bytes = [0_u8; 32];
    ring_rand::SystemRandom::new()
        .fill(&mut bytes)
        .map_err(|_| anyhow::anyhow!("secure random generation failed"))?;
    Ok(BASE64_URL_SAFE_NO_PAD.encode(bytes))
}

pub(super) fn hash(value: &[u8]) -> Vec<u8> {
    digest::digest(&digest::SHA256, value).as_ref().to_vec()
}

pub(super) fn encrypt(key: &[u8; 32], plaintext: &[u8]) -> anyhow::Result<Vec<u8>> {
    use ring_rand::SecureRandom as _;
    let key = aead::LessSafeKey::new(
        aead::UnboundKey::new(&aead::AES_256_GCM, key)
            .map_err(|_| anyhow::anyhow!("invalid token encryption key"))?,
    );
    let mut nonce_bytes = [0_u8; 12];
    ring_rand::SystemRandom::new()
        .fill(&mut nonce_bytes)
        .map_err(|_| anyhow::anyhow!("secure random generation failed"))?;
    let nonce = aead::Nonce::assume_unique_for_key(nonce_bytes);
    let mut output = plaintext.to_vec();
    key.seal_in_place_append_tag(nonce, aead::Aad::empty(), &mut output)
        .map_err(|_| anyhow::anyhow!("token encryption failed"))?;
    let mut encrypted = nonce_bytes.to_vec();
    encrypted.extend(output);
    Ok(encrypted)
}

pub(super) fn decrypt_string(key: &[u8; 32], encrypted: &[u8]) -> anyhow::Result<String> {
    if encrypted.len() < 12 + aead::AES_256_GCM.tag_len() {
        anyhow::bail!("encrypted token is truncated");
    }
    let (nonce, ciphertext) = encrypted.split_at(12);
    let key = aead::LessSafeKey::new(
        aead::UnboundKey::new(&aead::AES_256_GCM, key)
            .map_err(|_| anyhow::anyhow!("invalid token encryption key"))?,
    );
    let mut plaintext = ciphertext.to_vec();
    let plaintext = key
        .open_in_place(
            aead::Nonce::try_assume_unique_for_key(nonce)
                .map_err(|_| anyhow::anyhow!("invalid token nonce"))?,
            aead::Aad::empty(),
            &mut plaintext,
        )
        .map_err(|_| anyhow::anyhow!("token decryption failed"))?;
    Ok(String::from_utf8(plaintext.to_vec())?)
}

#[cfg(test)]
mod tests {
    use super::{decrypt_string, encrypt};

    #[test]
    fn encrypted_token_round_trips_and_rejects_tampering() {
        let key = [7; 32];
        let mut encrypted = encrypt(&key, b"secret").unwrap();
        assert_eq!(decrypt_string(&key, &encrypted).unwrap(), "secret");
        *encrypted.last_mut().unwrap() ^= 1;
        assert!(decrypt_string(&key, &encrypted).is_err());
    }
}
