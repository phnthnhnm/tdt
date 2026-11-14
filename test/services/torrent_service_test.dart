import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:path/path.dart' as p;
import 'package:tdt/services/torrent_service.dart';

import '../support/torrent_fixtures.dart';

void main() {
  late Directory tempDir;
  late TorrentService service;

  setUp(() async {
    tempDir = await Directory.systemTemp.createTemp('tdt_torrent_test_');
    service = TorrentService();
  });

  tearDown(() async {
    if (await tempDir.exists()) {
      await tempDir.delete(recursive: true);
    }
  });

  Future<String> writeTorrent(String fileName, List<int> bytes) async {
    final file = File(p.join(tempDir.path, fileName));
    await file.writeAsBytes(bytes);
    return file.path;
  }

  group('getFilesFromTorrent', () {
    test('reports every entry with the torrent root prefix', () async {
      final path = await writeTorrent(
        'pack.torrent',
        multiFileTorrent(
          name: 'Release',
          paths: [
            ['001.cbz'],
            ['extras', '002.cbz'],
          ],
        ),
      );

      final files = await service.getFilesFromTorrent(path);

      expect(files, {
        torrentFilePath('Release', ['001.cbz']),
        torrentFilePath('Release', ['extras', '002.cbz']),
      });
    });

    test('uses the torrent name for a single-file torrent', () async {
      final path = await writeTorrent(
        'solo.torrent',
        singleFileTorrent(name: 'chapter-01.cbz'),
      );

      final files = await service.getFilesFromTorrent(path);

      expect(files, {'chapter-01.cbz'});
    });

    test('throws when the file is not a torrent', () async {
      final path = await writeTorrent(
        'broken.torrent',
        'not bencode'.codeUnits,
      );

      // The parse error is raised inside the parser's isolate and forwarded as
      // the isolate's raw error value, so only "it throws" is guaranteed.
      await expectLater(service.getFilesFromTorrent(path), throwsA(anything));
    });
  });

  group('compare', () {
    test('sorts added and removed entries', () {
      final oldFiles = {'b.cbz', 'a.cbz', 'gone.cbz'};
      final newFiles = {'d.cbz', 'c.cbz', 'b.cbz'};

      final diff = service.compare(oldFiles, newFiles);

      expect(diff.keys, ['added', 'removed']);
      expect(diff['added'], ['c.cbz', 'd.cbz']);
      expect(diff['removed'], ['a.cbz', 'gone.cbz']);
    });

    test('returns two empty lists for identical sets', () {
      final diff = service.compare({'a.cbz', 'b.cbz'}, {'b.cbz', 'a.cbz'});

      expect(diff['added'], isEmpty);
      expect(diff['removed'], isEmpty);
    });

    test('reports only additions when nothing was removed', () {
      final diff = service.compare({'a.cbz'}, {'a.cbz', 'b.cbz'});

      expect(diff['added'], ['b.cbz']);
      expect(diff['removed'], isEmpty);
    });

    test('reports only removals when nothing was added', () {
      final diff = service.compare({'a.cbz', 'b.cbz'}, {'a.cbz'});

      expect(diff['added'], isEmpty);
      expect(diff['removed'], ['b.cbz']);
    });

    test('treats a renamed root as a full replacement', () {
      final diff = service.compare(
        {
          torrentFilePath('v1', ['001.cbz']),
        },
        {
          torrentFilePath('v2', ['001.cbz']),
        },
      );

      expect(diff['added'], [
        torrentFilePath('v2', ['001.cbz']),
      ]);
      expect(diff['removed'], [
        torrentFilePath('v1', ['001.cbz']),
      ]);
    });
  });
}
