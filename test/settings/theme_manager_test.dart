import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tdt/theme_manager.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('ThemeManager', () {
    test('defaults to system when nothing is stored', () async {
      SharedPreferences.setMockInitialValues({});

      await ThemeManager.init();

      expect(ThemeManager.themeMode.value, ThemeMode.system);
    });

    test('restores every stored mode', () async {
      for (final entry in {
        'light': ThemeMode.light,
        'dark': ThemeMode.dark,
        'system': ThemeMode.system,
      }.entries) {
        SharedPreferences.setMockInitialValues({'theme_mode': entry.key});

        await ThemeManager.init();

        expect(ThemeManager.themeMode.value, entry.value);
      }
    });

    test('falls back to system for an unknown value', () async {
      SharedPreferences.setMockInitialValues({'theme_mode': 'sepia'});

      await ThemeManager.init();

      expect(ThemeManager.themeMode.value, ThemeMode.system);
    });

    test('persists the selection under theme_mode', () async {
      SharedPreferences.setMockInitialValues({'theme_mode': 'dark'});
      await ThemeManager.init();

      await ThemeManager.setThemeMode(ThemeMode.light);

      expect(ThemeManager.themeMode.value, ThemeMode.light);
      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getString('theme_mode'), 'light');
    });

    test('round trips a selection through a restart', () async {
      SharedPreferences.setMockInitialValues({});
      await ThemeManager.init();
      await ThemeManager.setThemeMode(ThemeMode.dark);
      final stored = (await SharedPreferences.getInstance()).getString(
        'theme_mode',
      );
      expect(stored, 'dark');

      SharedPreferences.setMockInitialValues({'theme_mode': stored!});
      await ThemeManager.init();

      expect(ThemeManager.themeMode.value, ThemeMode.dark);
    });
  });
}
