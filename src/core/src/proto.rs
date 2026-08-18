// stdio 帧传输(IPC_PROTOCOL_V1 §1):4 字节 LE 长度前缀 + UTF-8 JSON
use std::io::{Read, Write};

pub fn send_frame<W: Write>(w: &mut W, value: &serde_json::Value) -> std::io::Result<()> {
    let body = serde_json::to_vec(value).map_err(std::io::Error::other)?;
    let mut frame = Vec::with_capacity(4 + body.len());
    frame.extend_from_slice(&(body.len() as u32).to_le_bytes());
    frame.extend_from_slice(&body);
    w.write_all(&frame)?;
    w.flush()
}

/// 从流中逐帧解析(带 carry 缓冲,处理半帧/粘帧)
pub struct FrameReader<R: Read> {
    reader: R,
    carry: Vec<u8>,
}

impl<R: Read> FrameReader<R> {
    pub fn new(reader: R) -> Self {
        Self {
            reader,
            carry: Vec::new(),
        }
    }

    /// 阻塞读一帧;EOF/错误 → None
    pub fn next(&mut self) -> Option<serde_json::Value> {
        let mut buf = [0u8; 64 * 1024];
        loop {
            match Self::take_frame(&mut self.carry) {
                Some(Some(v)) => return Some(v),
                Some(None) => continue, // 坏帧:跳过,继续读
                None => {}
            }
            let n = self.reader.read(&mut buf).ok()?;
            if n == 0 {
                return None;
            }
            self.carry.extend_from_slice(&buf[..n]);
        }
    }

    fn take_frame(carry: &mut Vec<u8>) -> Option<Option<serde_json::Value>> {
        if carry.len() < 4 {
            return None;
        }
        let len = u32::from_le_bytes([carry[0], carry[1], carry[2], carry[3]]) as usize;
        if carry.len() < 4 + len {
            return None;
        }
        let payload = carry[4..4 + len].to_vec();
        carry.drain(..4 + len);
        Some(serde_json::from_slice(&payload).ok())
    }
}
