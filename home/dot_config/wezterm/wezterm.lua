local wezterm = require "wezterm"

local config = wezterm.config_builder()

local function load_omarchy_colors()
  local home = os.getenv "HOME"
  if not home then
    return nil
  end

  local path = home .. "/.local/state/omarchy/current/theme/ghostty.conf"
  local file = io.open(path, "r")
  if not file then
    return nil
  end

  local colors = {}
  local palette = {}

  for line in file:lines() do
    local key, value = line:match "^([%w-]+)%s*=%s*(#[%x]+)%s*$"
    local index, color = line:match "^palette%s*=%s*(%d+)=(#[%x]+)%s*$"

    if key then
      colors[key] = value
    elseif index then
      palette[tonumber(index) + 1] = color
    end
  end

  file:close()

  if not colors.background or not colors.foreground or #palette ~= 16 then
    return nil
  end

  wezterm.add_to_config_reload_watch_list(path)

  return {
    foreground = colors.foreground,
    background = colors.background,
    cursor_bg = colors["cursor-color"] or colors.foreground,
    cursor_border = colors["cursor-color"] or colors.foreground,
    cursor_fg = colors.background,
    selection_bg = colors["selection-background"] or palette[9],
    selection_fg = colors["selection-foreground"] or colors.foreground,
    ansi = { table.unpack(palette, 1, 8) },
    brights = { table.unpack(palette, 9, 16) },
  }
end

config.font = wezterm.font("MonoLisa Nerd Font", { weight = "Regular" })
config.font_size = 11
config.adjust_window_size_when_changing_font_size = false

config.window_padding = { left = 14, right = 14, top = 14, bottom = 14 }
config.window_decorations = "TITLE|RESIZE"
config.window_close_confirmation = "NeverPrompt"
config.hide_tab_bar_if_only_one_tab = true
config.use_fancy_tab_bar = false

config.default_cursor_style = "SteadyBlock"
config.audible_bell = "Disabled"
config.hide_mouse_cursor_when_typing = true

-- Keep enough history for ordinary shells without duplicating Herdr's much
-- larger pane history.
config.scrollback_lines = 10000

-- WezTerm is managed by Scoop, so let the package manager handle updates.
config.check_for_updates = false

config.colors = load_omarchy_colors()
if not config.colors then
  config.color_scheme = "rose-pine"
end

if wezterm.target_triple:find "windows" then
  config.default_prog = { "herdr.exe" }
end

config.keys = {
  { key = "Insert", mods = "SHIFT", action = wezterm.action.PasteFrom "Clipboard" },
  { key = "Insert", mods = "CTRL", action = wezterm.action.CopyTo "Clipboard" },
  { key = "Enter", mods = "SHIFT", action = wezterm.action.SendString "\x1b[13;2u" },
  { key = "Enter", mods = "ALT|SHIFT", action = wezterm.action.SendString "\x1b[13;4u" },
}

return config
