// Simple, zero-dependency Node script to generate a valid Windows .ico file with multiple resolutions (16, 24, 32, 48, 64, 128, 256)
// Each resolution is stored as an uncompressed 32-bit BGRA DIB frame with 1-bit transparency mask.
import fs from 'node:fs';
import path from 'node:path';

function createIconFrame(size) {
  // We will render the cute earbud motif:
  // Black background circle / rounded rect, pale white earbuds, black eyes & smiles
  const width = size;
  const height = size;
  const pixels = Buffer.alloc(width * height * 4); // BGRA

  function setPixel(x, y, b, g, r, a) {
    if (x < 0 || x >= width || y < 0 || y >= height) return;
    // DIB bottom-up ordering
    const row = height - 1 - y;
    const idx = (row * width + x) * 4;
    // Alpha blending
    const srcA = a / 255;
    const destA = pixels[idx + 3] / 255;
    const outA = srcA + destA * (1 - srcA);
    if (outA > 0) {
      pixels[idx + 0] = Math.round((b * srcA + pixels[idx + 0] * destA * (1 - srcA)) / outA);
      pixels[idx + 1] = Math.round((g * srcA + pixels[idx + 1] * destA * (1 - srcA)) / outA);
      pixels[idx + 2] = Math.round((r * srcA + pixels[idx + 2] * destA * (1 - srcA)) / outA);
      pixels[idx + 3] = Math.round(outA * 255);
    }
  }

  function fillCircle(cx, cy, r, b, g, rCol, a = 255) {
    const r2 = r * r;
    const minX = Math.max(0, Math.floor(cx - r - 1));
    const maxX = Math.min(width - 1, Math.ceil(cx + r + 1));
    const minY = Math.max(0, Math.floor(cy - r - 1));
    const maxY = Math.min(height - 1, Math.ceil(cy + r + 1));

    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        const dx = x - cx;
        const dy = y - cy;
        const dist2 = dx * dx + dy * dy;
        if (dist2 <= r2) {
          const edgeDist = r - Math.sqrt(dist2);
          const edgeAlpha = Math.min(1, Math.max(0, edgeDist * 1.5)) * a;
          setPixel(x, y, b, g, rCol, edgeAlpha);
        }
      }
    }
  }

  function fillCapsule(x1, y1, x2, y2, radius, b, g, rCol, a = 255) {
    const minX = Math.max(0, Math.floor(Math.min(x1, x2) - radius - 1));
    const maxX = Math.min(width - 1, Math.ceil(Math.max(x1, x2) + radius + 1));
    const minY = Math.max(0, Math.floor(Math.min(y1, y2) - radius - 1));
    const maxY = Math.min(height - 1, Math.ceil(Math.max(y1, y2) + radius + 1));

    for (let y = minY; y <= maxY; y++) {
      for (let x = minX; x <= maxX; x++) {
        // Line segment distance
        const dx = x2 - x1;
        const dy = y2 - y1;
        const lenSq = dx * dx + dy * dy;
        let t = lenSq === 0 ? 0 : ((x - x1) * dx + (y - y1) * dy) / lenSq;
        t = Math.max(0, Math.min(1, t));
        const px = x1 + t * dx;
        const py = y1 + t * dy;
        const distSq = (x - px) * (x - px) + (y - py) * (y - py);
        if (distSq <= radius * radius) {
          const edgeDist = radius - Math.sqrt(distSq);
          const edgeAlpha = Math.min(1, Math.max(0, edgeDist * 1.5)) * a;
          setPixel(x, y, b, g, rCol, edgeAlpha);
        }
      }
    }
  }

  // 1. Background disc (dark rounded squircle/disc)
  fillCircle(width / 2, height / 2, size * 0.48, 18, 17, 13, 255);

  // 2. Connecting cable loop
  fillCapsule(size * 0.28, size * 0.72, size * 0.5, size * 0.88, size * 0.04, 30, 30, 30, 255);
  fillCapsule(size * 0.5, size * 0.88, size * 0.78, size * 0.74, size * 0.04, 30, 30, 30, 255);

  // 3. Left Earbud Stem & Head (pale white #f8fafc with #cbd5e1 tint)
  fillCapsule(size * 0.28, size * 0.45, size * 0.28, size * 0.70, size * 0.09, 15, 23, 15, 255); // outline
  fillCapsule(size * 0.28, size * 0.45, size * 0.28, size * 0.70, size * 0.065, 230, 235, 245, 255); // body

  fillCircle(size * 0.38, size * 0.36, size * 0.21, 15, 23, 15, 255); // head outline
  fillCircle(size * 0.38, size * 0.36, size * 0.18, 248, 250, 252, 255); // head fill

  // Left face: eyes and smile
  fillCircle(size * 0.32, size * 0.34, Math.max(1, size * 0.024), 20, 20, 20, 255);
  fillCircle(size * 0.44, size * 0.34, Math.max(1, size * 0.024), 20, 20, 20, 255);
  fillCapsule(size * 0.36, size * 0.40, size * 0.40, size * 0.40, Math.max(0.7, size * 0.016), 20, 20, 20, 255);

  // 4. Right Earbud Stem & Head (cute cat face)
  fillCapsule(size * 0.76, size * 0.60, size * 0.76, size * 0.82, size * 0.09, 15, 23, 15, 255); // outline
  fillCapsule(size * 0.76, size * 0.60, size * 0.76, size * 0.82, size * 0.065, 230, 235, 245, 255); // body

  fillCircle(size * 0.66, size * 0.54, size * 0.21, 15, 23, 15, 255); // head outline
  fillCircle(size * 0.66, size * 0.54, size * 0.18, 248, 250, 252, 255); // head fill

  // Right face: eyes and cute :3 mouth
  fillCircle(size * 0.60, size * 0.52, Math.max(1, size * 0.024), 20, 20, 20, 255);
  fillCircle(size * 0.72, size * 0.52, Math.max(1, size * 0.024), 20, 20, 20, 255);
  fillCapsule(size * 0.63, size * 0.58, size * 0.66, size * 0.60, Math.max(0.7, size * 0.015), 20, 20, 20, 255);
  fillCapsule(size * 0.66, size * 0.60, size * 0.69, size * 0.58, Math.max(0.7, size * 0.015), 20, 20, 20, 255);

  // AND mask (1 bit per pixel, row padded to 32 bits)
  const andRowBytes = Math.ceil(width / 32) * 4;
  const andMask = Buffer.alloc(andRowBytes * height, 0);

  // BITMAPINFOHEADER (40 bytes)
  // biHeight is 2 * height (DIB specification for icons with color + AND mask)
  const bih = Buffer.alloc(40);
  bih.writeUInt32LE(40, 0);           // biSize
  bih.writeInt32LE(width, 4);          // biWidth
  bih.writeInt32LE(height * 2, 8);     // biHeight = 2 * height
  bih.writeUInt16LE(1, 12);            // biPlanes
  bih.writeUInt16LE(32, 14);           // biBitCount = 32
  bih.writeUInt32LE(0, 16);            // biCompression = BI_RGB (0)
  bih.writeUInt32LE(pixels.length + andMask.length, 20); // biSizeImage
  bih.writeInt32LE(0, 24);             // biXPelsPerMeter
  bih.writeInt32LE(0, 28);             // biYPelsPerMeter
  bih.writeUInt32LE(0, 32);            // biClrUsed
  bih.writeUInt32LE(0, 36);            // biClrImportant

  const frameData = Buffer.concat([bih, pixels, andMask]);
  return {
    width: size === 256 ? 0 : size,
    height: size === 256 ? 0 : size,
    size: frameData.length,
    data: frameData,
  };
}

const sizes = [16, 24, 32, 48, 64, 128, 256];
const frames = sizes.map(createIconFrame);

// ICONDIR header: 6 bytes
// 0-1: 0 (reserved)
// 2-3: 1 (icon)
// 4-5: count (number of images)
const header = Buffer.alloc(6);
header.writeUInt16LE(0, 0);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(frames.length, 4);

let offset = 6 + frames.length * 16;
const entries = [];
for (const frame of frames) {
  const entry = Buffer.alloc(16);
  entry.writeUInt8(frame.width, 0);
  entry.writeUInt8(frame.height, 1);
  entry.writeUInt8(0, 2); // color count
  entry.writeUInt8(0, 3); // reserved
  entry.writeUInt16LE(1, 4); // color planes
  entry.writeUInt16LE(32, 6); // bpp
  entry.writeUInt32LE(frame.size, 8); // bytes in resource
  entry.writeUInt32LE(offset, 12); // image data offset
  entries.push(entry);
  offset += frame.size;
}

const icoBuffer = Buffer.concat([header, ...entries, ...frames.map(f => f.data)]);

const outDir = path.resolve('assets');
if (!fs.existsSync(outDir)) {
  fs.mkdirSync(outDir, { recursive: true });
}
const outPath = path.join(outDir, 'hoco-keepalive.ico');
fs.writeFileSync(outPath, icoBuffer);
console.log(`Successfully generated multi-resolution icon at ${outPath} (${icoBuffer.length} bytes, ${sizes.length} resolutions: ${sizes.join(', ')})`);
