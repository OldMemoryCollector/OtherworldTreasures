shader_type canvas_item;
render_mode unshaded;

uniform sampler2D gold_foil : source_color, filter_linear;
uniform float pulse = 0.0;
uniform float is_used = 0.0;
uniform float is_wax = 0.0;
uniform float silver_mode = 0.0;
varying vec4 item_tint;

void vertex() {
    item_tint = COLOR;
}

void fragment() {
    vec4 relic = texture(TEXTURE, UV);
    vec4 foil = texture(gold_foil, UV);
    float detail = dot(relic.rgb, vec3(0.299, 0.587, 0.114));

    // The original shading remains the principal source of shape and detail.
    vec3 dark_gold = vec3(0.25, 0.115, 0.025);
    vec3 mid_gold = vec3(0.88, 0.57, 0.12);
    vec3 light_gold = vec3(1.0, 0.93, 0.66);
    vec3 gold = mix(dark_gold, mid_gold, smoothstep(0.0, 0.62, detail));
    gold = mix(gold, light_gold, smoothstep(0.50, 1.0, detail));
    float foil_light = dot(foil.rgb, vec3(0.299, 0.587, 0.114));
    gold *= 1.0 + (foil_light - 0.50) * foil.a * 0.34;
    gold = mix(gold, light_gold, smoothstep(0.80, 1.0, foil_light) * foil.a * 0.20);
    vec3 silver = mix(vec3(0.12, 0.16, 0.22), vec3(0.70, 0.78, 0.86), smoothstep(0.0, 0.70, detail));
    silver = mix(silver, vec3(0.96, 0.98, 1.0), smoothstep(0.55, 1.0, detail));
    silver *= 1.0 + (foil_light - 0.50) * foil.a * 0.22;
    gold = mix(gold, silver, silver_mode);
    gold += abs(sin(TIME * 3.0)) * 0.2 * step(0.5, pulse);

    float gray = (gold.r + gold.g + gold.b) / 3.0;
    gold = mix(gold, vec3(gray), step(0.5, is_used));
    gold = mix(gold, vec3(gray) * vec3(1.0, 0.8, 0.8), step(0.5, is_wax));
    // The coating's background never determines the icon's silhouette.
    COLOR = vec4(gold * item_tint.rgb, relic.a * item_tint.a * 0.86);
}
