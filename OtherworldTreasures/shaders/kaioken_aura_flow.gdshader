shader_type canvas_item;
render_mode unshaded;
varying vec4 vertex_tint;

void vertex() {
    vertex_tint = COLOR;
}

void fragment() {
    vec2 uv = UV;
    // Small continuous deformation follows the upward flame, preserving its silhouette
    // and the transparent space for the character rather than scrolling the whole image.
    float edge = smoothstep(0.04, 0.25, UV.y) * (1.0 - smoothstep(0.88, 1.0, UV.y));
    uv.x += sin(UV.y * 19.0 - TIME * 2.0) * 0.008 * edge;
    uv.y += sin(UV.x * 15.0 + UV.y * 8.0 - TIME * 1.45) * 0.006 * edge;
    vec4 energy = texture(TEXTURE, uv);
    float flicker = 0.94 + 0.06 * sin(TIME * 3.6 + UV.y * 9.0);
    COLOR = vec4(energy.rgb * flicker, energy.a) * vertex_tint;
}
