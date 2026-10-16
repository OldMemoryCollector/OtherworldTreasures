// 魔贯光杀炮贯穿光束：用于 Line2D，UV.x 为沿线距离（像素）、UV.y 为横向 0..1。
// 参考原版 vfx_hyperbeam_laser_line 的手法：顶点扰动 + 滚动条纹 + LUT 式配色，
// 全部程序化生成，不依赖贴图。紫金螺旋：金色外缘、紫色螺旋条纹、白金核心。
shader_type canvas_item;
render_mode unshaded, blend_add;

uniform float line_length = 800.0;
uniform float scroll_speed = 900.0;
uniform float spiral_freq = 0.05;   // 每条螺旋条纹的像素周期
uniform float spiral_twist = 6.0;   // 沿横向的扭转圈数
uniform float wobble_speed = 20.0;
uniform float wobble_amp = 3.0;
uniform float fade = 1.0;
uniform vec4 edge_color : source_color = vec4(1.0, 0.78, 0.25, 1.0);
uniform vec4 mid_color : source_color = vec4(0.72, 0.25, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(1.0, 0.97, 0.9, 1.0);
varying vec4 item_tint;

void vertex() {
    item_tint = COLOR;
    // 能量流驱动的轻微整体波动（原版 VPO 顶点扰动的无贴图版）
    VERTEX.y += sin(UV.x * 0.02 + TIME * wobble_speed) * wobble_amp;
}

void fragment() {
    float u = UV.x / max(line_length, 1.0);
    float y = UV.y - 0.5;
    // 横向衰减：边缘柔和
    float across = 1.0 - abs(y) * 2.0;
    across = clamp(across, 0.0, 1.0);
    // 螺旋条纹：沿 beam 滚动，相位随横向扭转，收束于中心线
    float phase = (UV.x - TIME * scroll_speed) * spiral_freq + y * spiral_twist * 6.28318;
    float spiral = 0.5 + 0.5 * sin(phase);
    spiral = pow(spiral, 2.2);
    float core = pow(across, 3.5);
    float streaks = spiral * pow(across, 1.4);

    vec3 col = edge_color.rgb * pow(across, 1.1) * 0.55
             + mid_color.rgb * streaks * 1.1
             + core_color.rgb * core * 1.3;
    float a = clamp(pow(across, 1.3) * 0.55 + streaks * 0.75 + core, 0.0, 1.0);
    // 两端收细
    a *= smoothstep(0.0, 0.03, u) * (1.0 - smoothstep(0.97, 1.0, u) * 0.35);
    COLOR = vec4(col * item_tint.rgb, a * item_tint.a * fade);
}
