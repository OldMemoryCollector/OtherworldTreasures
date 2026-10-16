shader_type canvas_item;
render_mode unshaded, blend_add;

uniform float is_used = 0.0;
uniform vec4 glow_color : source_color = vec4(1.0, 0.83, 0.40, 1.0);
uniform float intensity = 0.72;
uniform float amplitude = 0.10;
uniform vec4 texture_region = vec4(0.0, 0.0, 1.0, 1.0);
varying vec4 item_tint;

void vertex() {
    item_tint = COLOR;
}

void fragment() {
    vec2 local_uv = (UV - texture_region.xy) / texture_region.zw;
    vec2 uv = (local_uv - vec2(0.5)) * 1.28 + vec2(0.5);
    float halo = 0.0;
    for (int ring = 1; ring <= 3; ring++) {
        float radius = float(ring) * 0.027;
        float sample_sum = 0.0;
        for (int ray = 0; ray < 8; ray++) {
            float angle = float(ray) * 0.785398163;
            vec2 sample_uv = uv + vec2(cos(angle), sin(angle)) * radius;
            float inside = step(0.0, sample_uv.x) * step(sample_uv.x, 1.0) * step(0.0, sample_uv.y) * step(sample_uv.y, 1.0);
            vec2 atlas_uv = texture_region.xy + clamp(sample_uv, vec2(0.0), vec2(1.0)) * texture_region.zw;
            atlas_uv = clamp(atlas_uv, texture_region.xy + TEXTURE_PIXEL_SIZE * 0.5, texture_region.xy + texture_region.zw - TEXTURE_PIXEL_SIZE * 0.5);
            sample_sum += texture(TEXTURE, atlas_uv).a * inside;
        }
        halo += sample_sum / 8.0 * (4.0 - float(ring)) / 6.0;
    }
    float breathing = intensity + amplitude * sin(TIME * 1.6);
    float alpha = halo * breathing * (1.0 - 0.80 * step(0.5, is_used));
    COLOR = vec4(glow_color.rgb * item_tint.rgb, alpha * item_tint.a * glow_color.a);
}
