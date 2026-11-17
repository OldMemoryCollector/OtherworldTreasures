shader_type canvas_item;
render_mode blend_mix, unshaded;

varying vec4 ribbon_modulate;
void vertex() {
    ribbon_modulate = COLOR;
}
void fragment() {
    // Use the complete original RGBA image, including its transparent space.
    COLOR = texture(TEXTURE, UV) * ribbon_modulate;
}
