import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:tdt/services/qbittorrent_service.dart';

import '../support/fake_http_client.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const host = '127.0.0.1';
  const port = 8080;

  group('QBittorrentService.initFromPrefs', () {
    test('applies a stored API key to later requests', () async {
      SharedPreferences.setMockInitialValues({'qbt_api_key': 'qbt_saved'});
      final client = FakeHttpClient((_) => jsonResponse([]));
      final service = QBittorrentService(client);

      await service.initFromPrefs();
      await service.getTorrents(host, port, false);

      expect(client.lastRequest.headers['Authorization'], 'Bearer qbt_saved');
    });

    test('leaves authentication untouched without a stored key', () async {
      SharedPreferences.setMockInitialValues({'qbt_api_key': ''});
      final client = FakeHttpClient((_) => jsonResponse([]));
      final service = QBittorrentService(client);

      await service.initFromPrefs();
      await service.getTorrents(host, port, false);

      expect(client.lastRequest.headers['Authorization'], isNull);
      expect(client.lastRequest.headers['Cookie'], isNull);
    });

    test('survives a store that has no key at all', () async {
      SharedPreferences.setMockInitialValues({
        'qbt_host': '127.0.0.1',
        'qbt_port': '8080',
      });
      final client = FakeHttpClient((_) => jsonResponse([]));
      final service = QBittorrentService(client);

      await service.initFromPrefs();
      await service.getTorrents(host, port, false);

      expect(client.lastRequest.headers['Authorization'], isNull);
    });
  });
}
