# Changelog

All notable changes to RdpAutoType are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/),
and the project uses [Semantic Versioning](https://semver.org/).

## [0.1.2] - 2026-11-02

### Added
- Live **search box** at the top of the cursor popup menu. Typing filters the
  whole database by title, username, URL and group path (all terms must match)
  and shows a flat result list, each with its usual Login / field actions.
  Results include the group path to disambiguate similar names; `Down`,
  `Enter` and `Esc` work from the box. An empty query restores the normal
  Recent + groups view.

## [0.1.1] - 2026-10-26

### Fixed
- Settings dialog layout is now DPI-robust (auto-sizing panels instead of fixed
  coordinates): the "Reset activation" button text is no longer clipped, the
  "Menu theme" label no longer overlaps its combo, OK/Cancel are aligned, and
  the "Clear" button on the Shortcut page is no longer cramped.

## [0.1.0] - 2026-10-25

### Added
- Initial public release: scan-code typing engine for simple and nested RDP,
  cursor popup menu, keyboard and mouse activators, visual Login Script editor,
  configurable timing and a configurable Edit Login Script shortcut,
  configurable popup menu theme (Auto / Light / Dark).
