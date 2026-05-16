import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';

import 'logging_client.dart';

class QBittorrentService {
  final http.Client _client;
  String? _cookie;
  bool _bypassLocalAuth = false;
  String? _apiKey;

  QBittorrentService([http.Client? client])
    : _client = client ?? LoggingClient();

  static final QBittorrentService shared = QBittorrentService();

  Future<void> initFromPrefs() async {
    final prefs = await SharedPreferences.getInstance();
    final apiKey = prefs.getString('qbt_api_key') ?? '';
    if (apiKey.isNotEmpty) setApiKey(apiKey);
  }

  void setApiKey(String? key) => _apiKey = key;

  Map<String, String> _buildHeaders(String base) {
    final headers = <String, String>{'Referer': base};
    if (_apiKey != null && _apiKey!.isNotEmpty) {
      headers['Authorization'] = 'Bearer ${_apiKey!}';
    } else if (!_bypassLocalAuth && _cookie != null) {
      headers['Cookie'] = _cookie!;
    }
    return headers;
  }

  String _baseUrl(String host, int port, bool useHttps) =>
      '${useHttps ? 'https' : 'http'}://$host:$port';

  Future<void> login(
    String host,
    int port,
    bool useHttps,
    String username,
    String password,
  ) async {
    if (_apiKey != null && _apiKey!.isNotEmpty) {
      throw Exception('Cannot call login when API key authentication is set');
    }
    final base = _baseUrl(host, port, useHttps);
    final uri = Uri.parse('$base/api/v2/auth/login');
    final res = await _client.post(
      uri,
      body: {'username': username, 'password': password},
      headers: {'Referer': base},
    );
    // qBittorrent 5.2.0 returns 204 on successful login (no content)
    if (res.statusCode != 200 && res.statusCode != 204) {
      throw Exception('qBittorrent login failed (${res.statusCode})');
    }
    final setCookie = res.headers['set-cookie'];
    if (setCookie != null) {
      _cookie = setCookie.split(';').first;
    }
  }

  Future<void> addTorrentBytes(
    String host,
    int port,
    bool useHttps,
    List<int> bytes, {
    String? savePath,
    bool paused = true,
  }) async {
    final base = _baseUrl(host, port, useHttps);
    final uri = Uri.parse('$base/api/v2/torrents/add');
    final request = http.MultipartRequest('POST', uri);
    request.headers.addAll(_buildHeaders(base));
    request.files.add(
      http.MultipartFile.fromBytes(
        'torrents',
        bytes,
        filename: 'upload.torrent',
      ),
    );
    if (savePath != null) {
      request.fields['savepath'] = savePath;
    }
    request.fields['paused'] = paused ? 'true' : 'false';

    final streamed = await _client.send(request);
    final resp = await http.Response.fromStream(streamed);
    if (resp.statusCode != 200) {
      throw Exception('Failed to add torrent (${resp.statusCode})');
    }
  }

  Future<List<Map<String, dynamic>>> getTorrents(
    String host,
    int port,
    bool useHttps,
  ) async {
    final base = _baseUrl(host, port, useHttps);
    final uri = Uri.parse('$base/api/v2/torrents/info');
    final res = await _client.get(uri, headers: _buildHeaders(base));
    if (res.statusCode != 200) {
      throw Exception('Failed to get torrents');
    }
    final List<dynamic> data = json.decode(res.body);
    return data.cast<Map<String, dynamic>>();
  }

  Future<List<Map<String, dynamic>>> getTorrentFiles(
    String host,
    int port,
    bool useHttps,
    String hash,
  ) async {
    final base = _baseUrl(host, port, useHttps);
    final uri = Uri.parse('$base/api/v2/torrents/files?hash=$hash');
    final res = await _client.get(uri, headers: _buildHeaders(base));
    if (res.statusCode != 200) {
      throw Exception('Failed to get torrent files');
    }
    final List<dynamic> data = json.decode(res.body);
    return data.cast<Map<String, dynamic>>();
  }

  Future<void> setFilePriority(
    String host,
    int port,
    bool useHttps,
    String hash,
    String ids,
    int priority,
  ) async {
    final base = _baseUrl(host, port, useHttps);
    final uri = Uri.parse('$base/api/v2/torrents/filePrio');

    final res = await _client.post(
      uri,
      body: {'hash': hash, 'id': ids, 'priority': priority.toString()},
      headers: _buildHeaders(base),
    );
    if (res.statusCode != 200) {
      throw Exception('Failed to set file priority (${res.statusCode})');
    }
  }

  Future<void> startTorrents(
    String host,
    int port,
    bool useHttps,
    String hashes,
  ) async {
    final base = _baseUrl(host, port, useHttps);
    final uriPost = Uri.parse('$base/api/v2/torrents/start');
    final res = await _client.post(
      uriPost,
      body: {'hashes': hashes},
      headers: _buildHeaders(base),
    );
    if (res.statusCode != 200) {
      throw Exception('Failed to start torrent(s) (${res.statusCode})');
    }
  }

  Future<void> pauseTorrents(
    String host,
    int port,
    bool useHttps,
    String hashes,
  ) async {
    final base = _baseUrl(host, port, useHttps);
    final uriPost = Uri.parse('$base/api/v2/torrents/stop');
    final res = await _client.post(
      uriPost,
      body: {'hashes': hashes},
      headers: _buildHeaders(base),
    );
    if (res.statusCode != 200) {
      throw Exception('Failed to pause torrent(s) (${res.statusCode})');
    }
  }

  Future<Map<String, dynamic>> getPreferences(
    String host,
    int port,
    bool useHttps,
  ) async {
    final base = _baseUrl(host, port, useHttps);
    final uri = Uri.parse('$base/api/v2/app/preferences');
    final res = await _client.get(uri, headers: _buildHeaders(base));
    if (res.statusCode != 200) {
      throw Exception('Failed to get preferences (${res.statusCode})');
    }
    final Map<String, dynamic> data = json.decode(res.body);
    if (data.containsKey('bypass_local_auth')) {
      _bypassLocalAuth = data['bypass_local_auth'] == true;
    }
    return data;
  }
}
