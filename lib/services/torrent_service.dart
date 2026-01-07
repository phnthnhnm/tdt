import 'package:dtorrent_parser/dtorrent_parser.dart';

class TorrentService {
  /// Reads a .torrent file and returns a Set of file paths inside it.
  Future<Set<String>> getFilesFromTorrent(String filePath) async {
    // dtorrent_parser parses the file and handles the binary decoding
    final Torrent model = await Torrent.parseFromFile(filePath);

    Set<String> filePaths = {};

    // Check if the torrent has multiple files (standard for comic packs)
    if (model.files.isNotEmpty) {
      for (var file in model.files) {
        // file.path is the full relative path (e.g. "comics/comic1.cbz")
        filePaths.add(file.path);
      }
    } else {
      // Fallback for single-file torrents
      filePaths.add(model.name);
    }

    return filePaths;
  }

  /// Compares two sets of files and returns the diff.
  Map<String, List<String>> compare(
    Set<String> oldFiles,
    Set<String> newFiles,
  ) {
    final added = newFiles.difference(oldFiles).toList();
    final removed = oldFiles.difference(newFiles).toList();

    added.sort();
    removed.sort();

    return {'added': added, 'removed': removed};
  }
}
