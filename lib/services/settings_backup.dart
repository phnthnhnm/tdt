import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

/// Typed `{type, value}` encoding of the stored preferences.
///
/// The Data tab uses this for backup and restore. The shape of the map is part
/// of the user-visible backup file format, so the type names stay stable.
class SettingsBackup {
  const SettingsBackup._();

  /// Encodes every readable preference into a JSON-compatible map.
  static Map<String, dynamic> encode(SharedPreferences prefs) {
    final dump = <String, dynamic>{};
    for (final key in prefs.getKeys()) {
      final value = prefs.get(key);
      if (value is bool) {
        dump[key] = {'type': 'bool', 'value': value};
      } else if (value is int) {
        dump[key] = {'type': 'int', 'value': value};
      } else if (value is double) {
        dump[key] = {'type': 'double', 'value': value};
      } else if (value is String) {
        dump[key] = {'type': 'string', 'value': value};
      } else if (value is List<String>) {
        dump[key] = {'type': 'stringList', 'value': value};
      }
    }
    return dump;
  }

  /// Encodes every readable preference as the JSON text written to a backup.
  static String encodeToText(SharedPreferences prefs) {
    return jsonEncode(encode(prefs));
  }

  /// Parses the contents of a backup file.
  ///
  /// Throws when the text is not a JSON object.
  static Map<String, dynamic> decode(String contents) {
    final decoded = jsonDecode(contents);
    if (decoded is! Map<String, dynamic>) {
      throw const FormatException('Backup file must contain a JSON object');
    }
    return decoded;
  }

  /// Replaces every stored preference with the entries of [data].
  ///
  /// The store is cleared before the entries are applied, so a malformed entry
  /// leaves the remaining preferences removed. That matches the Data tab's
  /// restore behavior.
  static Future<void> restore(
    SharedPreferences prefs,
    Map<String, dynamic> data,
  ) async {
    await prefs.clear();
    for (final entry in data.entries) {
      final value = entry.value as Map<String, dynamic>;
      final type = value['type'] as String?;
      final stored = value['value'];
      switch (type) {
        case 'bool':
          await prefs.setBool(entry.key, stored as bool);
        case 'int':
          await prefs.setInt(entry.key, stored as int);
        case 'double':
          await prefs.setDouble(entry.key, (stored as num).toDouble());
        case 'string':
          await prefs.setString(entry.key, stored as String);
        case 'stringList':
          await prefs.setStringList(
            entry.key,
            List<String>.from(stored as List),
          );
      }
    }
  }
}
