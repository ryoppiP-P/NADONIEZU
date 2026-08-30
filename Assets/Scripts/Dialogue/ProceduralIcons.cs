// 会話選択肢のルートアイコンを、外部画像を使わずコードだけで生成する。
// バカゲー感を出すためのちょっとした記号（ハート/丸/トゲトゲ/三角）を、
// 実行時にTexture2Dへピクセルを直接焼いてSprite化する。
using UnityEngine;

public static class ProceduralIcons {
    public static Sprite Get(RouteType route, Color color, int size = 96) {
        switch (route) {
            case RouteType.Romance: return Heart(color, size);
            case RouteType.Normal: return Circle(color, size);
            case RouteType.Madness: return Spike(color, size);
            case RouteType.Rebel: return Triangle(color, size);
            default: return Circle(color, size);
        }
    }

    public static Sprite Heart(Color color, int size = 96) {
        var tex = NewTex(size);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                // 中心を(0,0)、上向きをy正として正規化した座標系（-1.1〜1.1くらいに収まる）
                float nx = (x - half) / (half * 0.92f);
                float ny = (y - half) / (half * 0.92f) + 0.28f; // ハートの谷を中心よりやや上に

                // 古典的なハートの陰関数: (x²+y²-1)³ - x²y³ <= 0
                float x2 = nx * nx;
                float y2 = ny * ny;
                float lhs = (x2 + y2 - 1f);
                float val = lhs * lhs * lhs - x2 * ny * y2;

                float alpha = SoftEdge(val, 0f, 0.06f, true);
                SetPixelAA(tex, x, y, color, alpha);
            }
        }
        tex.Apply();
        return ToSprite(tex);
    }

    public static Sprite Circle(Color color, int size = 96) {
        var tex = NewTex(size);
        float half = size * 0.5f;
        float r = half * 0.78f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = SoftEdge(dist, r, 1.5f, false);
                SetPixelAA(tex, x, y, color, alpha);
            }
        }
        tex.Apply();
        return ToSprite(tex);
    }

    /// <summary>狂気ルート用：不揃いなトゲトゲの爆発マーク</summary>
    public static Sprite Spike(Color color, int size = 96) {
        var tex = NewTex(size);
        float half = size * 0.5f;
        int spikes = 8;
        // トゲの長さを角度ごとにランダムに変えて「歪な爆発」に見せる（シード固定で毎回同じ形にする）
        var rng = new System.Random(12345);
        float[] outerLen = new float[spikes];
        for (int i = 0; i < spikes; i++) outerLen[i] = half * (0.72f + 0.24f * (float)rng.NextDouble());
        float innerR = half * 0.38f;

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                if (ang < 0f) ang += Mathf.PI * 2f;

                float slice = (Mathf.PI * 2f) / spikes;
                int idx = Mathf.FloorToInt(ang / slice) % spikes;
                int idxNext = (idx + 1) % spikes;
                float tLocal = (ang - idx * slice) / slice; // 0..1 このスライス内の位置

                // トゲの先端(idxのouterLen)から谷(innerR)を経て次のトゲへ、三角波で結ぶ
                float edgeRadius;
                if (tLocal < 0.5f)
                    edgeRadius = Mathf.Lerp(outerLen[idx], innerR, tLocal / 0.5f);
                else
                    edgeRadius = Mathf.Lerp(innerR, outerLen[idxNext], (tLocal - 0.5f) / 0.5f);

                float alpha = SoftEdge(dist, edgeRadius, 1.5f, false);
                SetPixelAA(tex, x, y, color, alpha);
            }
        }
        tex.Apply();
        return ToSprite(tex);
    }

    /// <summary>反抗ルート用：上向きの警告三角</summary>
    /// <summary>反抗ルートの常時エフェクト用：ジグザグの稲妻</summary>
    public static Sprite Bolt(Color color, int size = 96) {
        var tex = NewTex(size);
        // 稲妻の折れ線（0〜1の正規化座標）。上から下へジグザグに落ちる形
        Vector2[] pts = new Vector2[] {
            new Vector2(0.62f, 0.95f),
            new Vector2(0.30f, 0.55f),
            new Vector2(0.50f, 0.55f),
            new Vector2(0.20f, 0.05f),
            new Vector2(0.62f, 0.45f),
            new Vector2(0.42f, 0.45f),
            new Vector2(0.78f, 0.85f),
            new Vector2(0.62f, 0.95f),
        };
        for (int i = 0; i < pts.Length; i++) pts[i] *= size;

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = PointInPolygon(p, pts);
                float alpha = inside ? 1f : SoftEdgeToPolygon(p, pts, 1.2f);
                SetPixelAA(tex, x, y, color, alpha);
            }
        }
        tex.Apply();
        return ToSprite(tex);
    }

    /// <summary>狂気ルートの常時エフェクト用：中心から外へじんわり滲む不穏な後光</summary>
    public static Sprite SoftAura(Color color, int size = 128) {
        var tex = NewTex(size);
        float half = size * 0.5f;
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = x - half + 0.5f;
                float dy = y - half + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / half; // 0(中心)〜1(外周)
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha; // 中心に寄せて外側は早めに透明化
                SetPixelAA(tex, x, y, color, alpha);
            }
        }
        tex.Apply();
        return ToSprite(tex);
    }

    static bool PointInPolygon(Vector2 p, Vector2[] poly) {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) {
                inside = !inside;
            }
        }
        return inside;
    }

    static float SoftEdgeToPolygon(Vector2 p, Vector2[] poly, float width) {
        float minDist = float.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) {
            minDist = Mathf.Min(minDist, MinDistToSegment(p, poly[j], poly[i]));
        }
        return Mathf.Clamp01(1f - minDist / width);
    }

    public static Sprite Triangle(Color color, int size = 96) {
        var tex = NewTex(size);
        float half = size * 0.5f;
        // 正三角形の3頂点（上・左下・右下）
        Vector2 top = new Vector2(half, size * 0.88f);
        Vector2 bl = new Vector2(size * 0.12f, size * 0.12f);
        Vector2 br = new Vector2(size * 0.88f, size * 0.12f);

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = SignedDistTriangle(p, top, bl, br);
                float alpha = SoftEdge(d, 0f, 1.5f, true);
                SetPixelAA(tex, x, y, color, alpha);
            }
        }
        tex.Apply();
        return ToSprite(tex);
    }

    // ===== 内部ヘルパー =====

    static Texture2D NewTex(int size) {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    static Sprite ToSprite(Texture2D tex) {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
    }

    /// <summary>value(距離や陰関数値)がedge未満なら不透明、edge付近をwidthでなめらかにぼかす。
    /// invert=trueならvalueが小さいほど内側（ハート/三角の陰関数向け）。</summary>
    static float SoftEdge(float value, float edge, float width, bool invert) {
        float t = (value - edge) / Mathf.Max(width, 0.0001f);
        float a = Mathf.Clamp01(0.5f - t); // t<<0で1、t>>0で0
        return invert ? a : a;
    }

    static void SetPixelAA(Texture2D tex, int x, int y, Color color, float alpha) {
        var c = color;
        c.a = color.a * alpha;
        tex.SetPixel(x, y, c);
    }

    /// <summary>点pと三角形(a,b,c)の符号付き距離。三角形の内側は負、外側は正になるようにする。</summary>
    static float SignedDistTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) {
        float d1 = EdgeSide(p, a, b);
        float d2 = EdgeSide(p, b, c);
        float d3 = EdgeSide(p, c, a);
        bool inside = (d1 >= 0f && d2 >= 0f && d3 >= 0f) || (d1 <= 0f && d2 <= 0f && d3 <= 0f);

        float dist = MinDistToSegment(p, a, b);
        dist = Mathf.Min(dist, MinDistToSegment(p, b, c));
        dist = Mathf.Min(dist, MinDistToSegment(p, c, a));

        return inside ? -dist : dist;
    }

    static float EdgeSide(Vector2 p, Vector2 a, Vector2 b) {
        return (p.x - a.x) * (b.y - a.y) - (p.y - a.y) * (b.x - a.x);
    }

    static float MinDistToSegment(Vector2 p, Vector2 a, Vector2 b) {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f);
        t = Mathf.Clamp01(t);
        Vector2 proj = a + ab * t;
        return Vector2.Distance(p, proj);
    }
}
