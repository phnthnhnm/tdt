import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

/// Bencoded `.torrent` fixtures built in process, so the tests do not depend on
/// checked-in binary files.
///
/// A multi-file torrent lists one entry per file; a single-file torrent puts
/// `length` directly in `info`.
Uint8List multiFileTorrent({
  required String name,
  required List<List<String>> paths,
  int pieceLength = 16384,
}) {
  return _torrent(<String, Object?>{
    'name': name,
    'piece length': pieceLength,
    'pieces': _pieceHashes(1),
    'files': <Object?>[
      for (final path in paths) <String, Object?>{'length': 1024, 'path': path},
    ],
  });
}

Uint8List singleFileTorrent({
  required String name,
  int length = 4096,
  int pieceLength = 16384,
}) {
  return _torrent(<String, Object?>{
    'name': name,
    'piece length': pieceLength,
    'pieces': _pieceHashes(1),
    'length': length,
  });
}

/// The path the parser reports for file [segments] inside a torrent [name].
String torrentFilePath(String name, List<String> segments) {
  return <String>[name, ...segments].join(Platform.pathSeparator);
}

Uint8List _torrent(Map<String, Object?> info) {
  return Uint8List.fromList(
    bencode(<String, Object?>{
      'announce': 'http://tracker.example/announce',
      'info': info,
    }),
  );
}

List<int> _pieceHashes(int count) {
  return List<int>.generate(count * 20, (index) => index % 256);
}

/// Encodes [value] as bencode, sorting map keys as the format requires.
List<int> bencode(Object? value) {
  final out = <int>[];
  _encode(value, out);
  return out;
}

void _encode(Object? value, List<int> out) {
  if (value is int) {
    out.addAll(ascii.encode('i${value}e'));
  } else if (value is String) {
    final bytes = utf8.encode(value);
    out.addAll(ascii.encode('${bytes.length}:'));
    out.addAll(bytes);
  } else if (value is List<int>) {
    out.addAll(ascii.encode('${value.length}:'));
    out.addAll(value);
  } else if (value is List) {
    out.add(0x6c);
    for (final item in value) {
      _encode(item, out);
    }
    out.add(0x65);
  } else if (value is Map<String, Object?>) {
    out.add(0x64);
    final keys = value.keys.toList()..sort();
    for (final key in keys) {
      _encode(key, out);
      _encode(value[key], out);
    }
    out.add(0x65);
  } else {
    throw ArgumentError('cannot bencode ${value.runtimeType}');
  }
}
