import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tdt/services/settings_backup.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  Future<SharedPreferences> prefsWith(Map<String, Object> values) async {
    SharedPreferences.setMockInitialValues(values);
    return SharedPreferences.getInstance();
  }

  group('encode', () {
    test('tags every supported preference type', () async {
      final prefs = await prefsWith({
        'qbt_use_https': true,
        'qbt_port': 8080,
        'scale': 1.5,
        'qbt_host': '127.0.0.1',
        'qbt_blacklist': <String>['sample', 'xxx'],
      });

      final dump = SettingsBackup.encode(prefs);

      expect(dump['qbt_use_https'], {'type': 'bool', 'value': true});
      expect(dump['qbt_port'], {'type': 'int', 'value': 8080});
      expect(dump['scale'], {'type': 'double', 'value': 1.5});
      expect(dump['qbt_host'], {'type': 'string', 'value': '127.0.0.1'});
      expect(dump['qbt_blacklist'], {
        'type': 'stringList',
        'value': ['sample', 'xxx'],
      });
    });

    test('produces JSON text that decodes again', () async {
      final prefs = await prefsWith({'qbt_host': 'nas.local'});

      final text = SettingsBackup.encodeToText(prefs);
      final decoded = SettingsBackup.decode(text);

      expect(decoded['qbt_host'], {'type': 'string', 'value': 'nas.local'});
    });
  });

  group('decode', () {
    test('rejects text that is not a JSON object', () {
      expect(() => SettingsBackup.decode('[]'), throwsFormatException);
      expect(() => SettingsBackup.decode('not json'), throwsFormatException);
    });
  });

  group('restore', () {
    test('replaces the whole store', () async {
      final prefs = await prefsWith({'stale': 'remove me', 'qbt_host': 'old'});

      await SettingsBackup.restore(prefs, {
        'qbt_host': {'type': 'string', 'value': 'new'},
      });

      expect(prefs.getKeys(), {'qbt_host'});
      expect(prefs.getString('qbt_host'), 'new');
    });

    test('applies every supported type', () async {
      final prefs = await prefsWith({});

      await SettingsBackup.restore(prefs, {
        'qbt_use_https': {'type': 'bool', 'value': true},
        'qbt_port': {'type': 'int', 'value': 9091},
        'scale': {'type': 'double', 'value': 2},
        'qbt_host': {'type': 'string', 'value': 'seedbox'},
        'qbt_blacklist': {
          'type': 'stringList',
          'value': ['sample'],
        },
      });

      expect(prefs.getBool('qbt_use_https'), isTrue);
      expect(prefs.getInt('qbt_port'), 9091);
      expect(prefs.getDouble('scale'), 2.0);
      expect(prefs.getString('qbt_host'), 'seedbox');
      expect(prefs.getStringList('qbt_blacklist'), ['sample']);
    });

    test('ignores entries with an unknown type', () async {
      final prefs = await prefsWith({});

      await SettingsBackup.restore(prefs, {
        'future_key': {'type': 'object', 'value': <String, Object>{}},
      });

      expect(prefs.getKeys(), isEmpty);
    });

    test('clears the store before a malformed entry fails', () async {
      final prefs = await prefsWith({'qbt_host': 'old'});

      await expectLater(
        SettingsBackup.restore(prefs, {
          'qbt_port': {'type': 'int', 'value': 'not a number'},
        }),
        throwsA(isA<TypeError>()),
      );

      expect(prefs.getKeys(), isEmpty);
    });

    test('round trips a backup built from a live store', () async {
      final prefs = await prefsWith({
        'theme_mode': 'dark',
        'qbt_port': '8080',
        'qbt_blacklist': <String>['sample'],
      });
      final text = SettingsBackup.encodeToText(prefs);

      SharedPreferences.setMockInitialValues({'theme_mode': 'light'});
      final restored = await SharedPreferences.getInstance();
      await SettingsBackup.restore(restored, SettingsBackup.decode(text));

      expect(restored.getString('theme_mode'), 'dark');
      expect(restored.getString('qbt_port'), '8080');
      expect(restored.getStringList('qbt_blacklist'), ['sample']);
    });
  });
}
