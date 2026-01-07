import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:logging/logging.dart';

class LoggingClient extends http.BaseClient {
  final http.Client _inner;
  final Logger _logger;

  LoggingClient([http.Client? inner, Logger? logger])
    : _inner = inner ?? http.Client(),
      _logger = logger ?? Logger('LoggingClient');

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final safeHeaders = Map<String, String>.from(request.headers);
    safeHeaders.remove('Cookie');
    safeHeaders.remove('Authorization');

    _logger.info('➡ ${request.method} ${request.url}');
    if (kDebugMode) {
      _logger.fine('Headers: $safeHeaders');
    }

    if (kDebugMode) {
      try {
        if (request is http.Request) {
          final body = request.body;
          if (body.isNotEmpty) {
            final snippet = body.length > 2000
                ? '${body.substring(0, 2000)}...'
                : body;
            _logger.fine('Body: $snippet');
          }
        } else if (request is http.MultipartRequest) {
          _logger.fine('Multipart fields: ${request.fields}');
          _logger.fine(
            'Multipart files: ${request.files.map((f) => f.filename).toList()}',
          );
        }
      } catch (e) {
        _logger.warning('Failed to read request body for logging: $e');
      }
    }

    final start = DateTime.now();
    final streamed = await _inner.send(request);
    final response = await http.Response.fromStream(streamed);
    final elapsed = DateTime.now().difference(start);

    final bodySnippet = response.body.length > 2000
        ? '${response.body.substring(0, 2000)}...'
        : response.body;
    _logger.info(
      '⬅ ${response.statusCode} ${request.url} (${elapsed.inMilliseconds}ms)',
    );
    if (kDebugMode) {
      _logger.fine('Response headers: ${response.headers}');
      _logger.fine('Response body: $bodySnippet');
    }

    // Reconstruct StreamedResponse so callers can still read the body
    return http.StreamedResponse(
      Stream.fromIterable([response.bodyBytes]),
      response.statusCode,
      headers: response.headers,
      reasonPhrase: response.reasonPhrase,
      request: response.request,
    );
  }

  @override
  void close() => _inner.close();
}
