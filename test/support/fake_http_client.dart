import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

/// Scripted [http.Client] used to observe the requests a service builds.
///
/// The handler runs for every request; the recorded requests keep their
/// concrete types, so a test can read `bodyFields` from a form post or
/// `fields`/`files` from a multipart upload.
class FakeHttpClient extends http.BaseClient {
  FakeHttpClient(this._handler);

  final FutureOr<http.StreamedResponse> Function(http.BaseRequest request)
  _handler;

  final List<http.BaseRequest> requests = <http.BaseRequest>[];

  http.BaseRequest get lastRequest => requests.last;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    requests.add(request);
    return _handler(request);
  }
}

/// A response carrying a JSON encoded body.
http.StreamedResponse jsonResponse(
  Object? body, {
  int statusCode = 200,
  Map<String, String> headers = const <String, String>{},
}) {
  return http.StreamedResponse(
    Stream<List<int>>.value(utf8.encode(jsonEncode(body))),
    statusCode,
    headers: headers,
  );
}

/// An empty response, as returned by the qBittorrent write endpoints.
http.StreamedResponse emptyResponse({
  int statusCode = 200,
  Map<String, String> headers = const <String, String>{},
}) {
  return http.StreamedResponse(
    const Stream<List<int>>.empty(),
    statusCode,
    headers: headers,
  );
}
