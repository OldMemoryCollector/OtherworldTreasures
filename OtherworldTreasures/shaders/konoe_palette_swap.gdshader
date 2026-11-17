shader_type canvas_item;
render_mode unshaded;

// 奈茵战斗本体换色：两张 64x1 调色板条，像素 i 就是调色板索引 i 的颜色。
// 73 张战斗帧的不透明像素已核对 100% 落在 ph00_00[0..63] 内，反查索引后到目标表取色。
// 只改 Sprite2D 的 Material，不与逐帧动画状态机每帧覆写 Texture 冲突。
// 颜色空间：内置 TEXTURE 采样返回 linear，自定义 sampler2D 要靠 source_color 才做 sRGB->linear，
// 否则实机 GPU 上 ref_color 与 src.rgb 永远对不上，换色静默失效（headless dummy 测不出）。
// 下面仍保留一次兜底：若首轮一个都匹配不上，按 sRGB 手动转换再比一轮。
uniform sampler2D default_palette : source_color, filter_nearest, repeat_disable;
uniform sampler2D target_palette : source_color, filter_nearest, repeat_disable;

vec3 to_linear(vec3 c)
{
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void fragment()
{
	vec4 src = texture(TEXTURE, UV);
	COLOR = src;
	if (src.a >= 0.01)
	{
		int best = -1;
		float best_dist = 0.08;
		bool hint_works = true;
		for (int i = 0; i < 64; i++)
		{
			float u = (float(i) + 0.5) / 64.0;
			float d = length(texture(default_palette, vec2(u, 0.5)).rgb - src.rgb);
			if (d < best_dist)
			{
				best_dist = d;
				best = i;
			}
		}
		if (best < 0)
		{
			// source_color 没生效：调色板采样仍是 sRGB，手动转 linear 再比一次
			hint_works = false;
			for (int i = 0; i < 64; i++)
			{
				float u = (float(i) + 0.5) / 64.0;
				if (length(to_linear(texture(default_palette, vec2(u, 0.5)).rgb) - src.rgb) < 0.08)
				{
					best = i;
					break;
				}
			}
		}
		if (best >= 0)
		{
			float u = (float(best) + 0.5) / 64.0;
			vec3 col = texture(target_palette, vec2(u, 0.5)).rgb;
			if (!hint_works) col = to_linear(col);
			COLOR = vec4(col, src.a);
		}
	}
}
