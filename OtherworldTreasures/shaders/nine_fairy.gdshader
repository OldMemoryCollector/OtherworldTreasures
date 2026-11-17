shader_type canvas_item;
render_mode blend_add, unshaded;
varying vec4 fairy_modulate;
void vertex() { fairy_modulate = COLOR; }
void fragment() {
    vec4 source = texture(TEXTURE, UV);
    vec3 background = texture(TEXTURE, vec2(0.0, 0.0)).rgb;
    vec3 glow = max(source.rgb - background, vec3(0.0)) * 2.4;
    COLOR = vec4(glow, source.a) * fairy_modulate;
}
