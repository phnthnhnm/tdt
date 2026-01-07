import 'package:flutter/material.dart';

import '../../theme_manager.dart';

class AppearanceTab extends StatelessWidget {
  const AppearanceTab({super.key});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: ThemeManager.themeMode,
      builder: (context, mode, _) {
        return ListView(
          padding: const EdgeInsets.all(16.0),
          children: [
            const Text(
              'Theme',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            RadioListTile<ThemeMode>(
              value: ThemeMode.system,
              groupValue: mode,
              title: const Text('System'),
              onChanged: (v) {
                if (v != null) ThemeManager.setThemeMode(v);
              },
            ),
            RadioListTile<ThemeMode>(
              value: ThemeMode.light,
              groupValue: mode,
              title: const Text('Light'),
              onChanged: (v) {
                if (v != null) ThemeManager.setThemeMode(v);
              },
            ),
            RadioListTile<ThemeMode>(
              value: ThemeMode.dark,
              groupValue: mode,
              title: const Text('Dark'),
              onChanged: (v) {
                if (v != null) ThemeManager.setThemeMode(v);
              },
            ),
          ],
        );
      },
    );
  }
}
