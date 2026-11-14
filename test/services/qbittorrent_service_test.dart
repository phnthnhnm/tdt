import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:tdt/services/qbittorrent_service.dart';

import '../support/fake_http_client.dart';

void main() {
  const host = '127.0.0.1';
  const port = 8080;
  const base = 'http://127.0.0.1:8080';

  group('login', () {
    test('posts form credentials and keeps the first cookie segment', () async {
      final client = FakeHttpClient((request) {
        if (request.url.path.endsWith('/auth/login')) {
          return emptyResponse(
            headers: const {'set-cookie': 'SID=abc123; HttpOnly; path=/'},
          );
        }
        return jsonResponse([]);
      });
      final service = QBittorrentService(client);

      await service.login(host, port, false, 'admin', 'secret');

      final request = client.requests.first;
      expect(request.method, 'POST');
      expect(request.url.toString(), '$base/api/v2/auth/login');
      expect(request.headers['Referer'], base);
      expect(request.headers['Cookie'], isNull);
      expect((request as http.Request).bodyFields, {
        'username': 'admin',
        'password': 'secret',
      });

      await service.getTorrents(host, port, false);
      expect(client.lastRequest.headers['Cookie'], 'SID=abc123');
    });

    test('accepts 204 from qBittorrent 5.2 and later', () async {
      final client = FakeHttpClient((request) {
        if (request.url.path.endsWith('/auth/login')) {
          return emptyResponse(
            statusCode: 204,
            headers: const {'set-cookie': 'SID=xyz; path=/'},
          );
        }
        return jsonResponse([]);
      });
      final service = QBittorrentService(client);

      await service.login(host, port, false, 'u', 'p');
      await service.getTorrents(host, port, false);

      expect(client.lastRequest.headers['Cookie'], 'SID=xyz');
    });

    test('throws with the status when the credentials are rejected', () async {
      final client = FakeHttpClient((_) => emptyResponse(statusCode: 403));

      await expectLater(
        QBittorrentService(client).login(host, port, false, 'u', 'p'),
        throwsA(
          isA<Exception>().having(
            (e) => e.toString(),
            'message',
            contains('403'),
          ),
        ),
      );
    });

    test('refuses to log in when an API key is configured', () async {
      final client = FakeHttpClient((_) => emptyResponse());
      final service = QBittorrentService(client)..setApiKey('qbt_key');

      await expectLater(
        service.login(host, port, false, 'u', 'p'),
        throwsA(isA<Exception>()),
      );
      expect(client.requests, isEmpty);
    });
  });

  group('authentication headers', () {
    test('sends a bearer token when an API key is set', () async {
      final client = FakeHttpClient((_) => jsonResponse([]));
      final service = QBittorrentService(client)..setApiKey('qbt_secret');

      await service.getTorrents(host, port, false);

      final headers = client.lastRequest.headers;
      expect(headers['Authorization'], 'Bearer qbt_secret');
      expect(headers['Cookie'], isNull);
    });

    test('omits the cookie once the server bypasses local auth', () async {
      final client = FakeHttpClient((request) {
        if (request.url.path.endsWith('/auth/login')) {
          return emptyResponse(
            headers: const {'set-cookie': 'SID=abc123; path=/'},
          );
        }
        if (request.url.path.endsWith('/app/preferences')) {
          return jsonResponse({'bypass_local_auth': true});
        }
        return jsonResponse([]);
      });
      final service = QBittorrentService(client);
      await service.login(host, port, false, 'u', 'p');

      await service.getPreferences(host, port, false);
      expect(
        client.lastRequest.headers['Cookie'],
        'SID=abc123',
        reason: 'the preferences call itself still carries the cookie',
      );

      await service.getTorrents(host, port, false);
      expect(client.lastRequest.headers['Cookie'], isNull);
    });

    test('keeps sending the cookie when local auth is not bypassed', () async {
      final client = FakeHttpClient((request) {
        if (request.url.path.endsWith('/auth/login')) {
          return emptyResponse(
            headers: const {'set-cookie': 'SID=abc123; path=/'},
          );
        }
        if (request.url.path.endsWith('/app/preferences')) {
          return jsonResponse({'bypass_local_auth': false});
        }
        return jsonResponse([]);
      });
      final service = QBittorrentService(client);
      await service.login(host, port, false, 'u', 'p');
      await service.getPreferences(host, port, false);

      await service.getTorrents(host, port, false);
      expect(client.lastRequest.headers['Cookie'], 'SID=abc123');
    });

    test('switches to https and keeps the referer in sync', () async {
      final client = FakeHttpClient((_) => jsonResponse([]));

      await QBittorrentService(client).getTorrents(host, port, true);

      expect(
        client.lastRequest.url.toString(),
        'https://127.0.0.1:8080/api/v2/torrents/info',
      );
      expect(client.lastRequest.headers['Referer'], 'https://127.0.0.1:8080');
    });
  });

  group('torrent queries', () {
    test('decodes the torrent list', () async {
      final client = FakeHttpClient(
        (_) => jsonResponse([
          {'name': 'Release', 'hash': 'abc'},
        ]),
      );

      final torrents = await QBittorrentService(
        client,
      ).getTorrents(host, port, false);

      expect(torrents, hasLength(1));
      expect(torrents.single['hash'], 'abc');
    });

    test('throws on a non-200 torrent list', () async {
      final client = FakeHttpClient((_) => emptyResponse(statusCode: 500));

      await expectLater(
        QBittorrentService(client).getTorrents(host, port, false),
        throwsA(isA<Exception>()),
      );
    });

    test('requests files for a hash', () async {
      final client = FakeHttpClient(
        (_) => jsonResponse([
          {'index': 0, 'name': 'Release/001.cbz'},
        ]),
      );

      final files = await QBittorrentService(
        client,
      ).getTorrentFiles(host, port, false, 'deadbeef');

      expect(client.lastRequest.url.query, 'hash=deadbeef');
      expect(files.single['index'], 0);
    });

    test('throws on a non-200 file list', () async {
      final client = FakeHttpClient((_) => emptyResponse(statusCode: 404));

      await expectLater(
        QBittorrentService(client).getTorrentFiles(host, port, false, 'abc'),
        throwsA(isA<Exception>()),
      );
    });
  });

  group('torrent mutations', () {
    test(
      'uploads the torrent as multipart with save path and paused flag',
      () async {
        final client = FakeHttpClient((_) => emptyResponse());
        final service = QBittorrentService(client);

        await service.addTorrentBytes(
          host,
          port,
          false,
          const [1, 2, 3],
          savePath: r'D:\Downloads',
          paused: false,
        );

        final request = client.lastRequest;
        expect(request.method, 'POST');
        expect('$base/api/v2/torrents/add', request.url.toString());
        expect(request.headers['Referer'], base);
        final multipart = request as http.MultipartRequest;
        expect(multipart.fields['savepath'], r'D:\Downloads');
        expect(multipart.fields['paused'], 'false');
        expect(multipart.files.single.field, 'torrents');
        expect(multipart.files.single.filename, 'upload.torrent');
      },
    );

    test('defaults to paused and omits the save path', () async {
      final client = FakeHttpClient((_) => emptyResponse());

      await QBittorrentService(
        client,
      ).addTorrentBytes(host, port, false, const [1, 2, 3]);

      final multipart = client.lastRequest as http.MultipartRequest;
      expect(multipart.fields['paused'], 'true');
      expect(multipart.fields.containsKey('savepath'), isFalse);
    });

    test('throws when the torrent is rejected', () async {
      final client = FakeHttpClient((_) => emptyResponse(statusCode: 415));

      await expectLater(
        QBittorrentService(
          client,
        ).addTorrentBytes(host, port, false, const [1]),
        throwsA(isA<Exception>()),
      );
    });

    test('posts file priorities', () async {
      final client = FakeHttpClient((_) => emptyResponse());

      await QBittorrentService(
        client,
      ).setFilePriority(host, port, false, 'abc', '0|2|5', 1);

      final request = client.lastRequest;
      expect(request.url.path, '/api/v2/torrents/filePrio');
      expect((request as http.Request).bodyFields, {
        'hash': 'abc',
        'id': '0|2|5',
        'priority': '1',
      });
    });

    test('throws when file priorities are rejected', () async {
      final client = FakeHttpClient((_) => emptyResponse(statusCode: 400));

      await expectLater(
        QBittorrentService(
          client,
        ).setFilePriority(host, port, false, 'abc', '0', 0),
        throwsA(isA<Exception>()),
      );
    });

    test('starts and pauses torrents through the v2 endpoints', () async {
      final client = FakeHttpClient((_) => emptyResponse());
      final service = QBittorrentService(client);

      await service.startTorrents(host, port, false, 'abc');
      expect(client.lastRequest.url.path, '/api/v2/torrents/start');
      expect((client.lastRequest as http.Request).bodyFields, {
        'hashes': 'abc',
      });

      await service.pauseTorrents(host, port, false, 'abc');
      expect(client.lastRequest.url.path, '/api/v2/torrents/stop');
      expect((client.lastRequest as http.Request).bodyFields, {
        'hashes': 'abc',
      });
    });

    test('throws when pausing is rejected', () async {
      final client = FakeHttpClient((_) => emptyResponse(statusCode: 409));

      await expectLater(
        QBittorrentService(client).pauseTorrents(host, port, false, 'abc'),
        throwsA(isA<Exception>()),
      );
    });
  });
}
