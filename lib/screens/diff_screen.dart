import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../torrent_service.dart';
import '../widgets/file_row.dart';
import '../widgets/result_list.dart';

class DiffScreen extends StatefulWidget {
  const DiffScreen({super.key});

  @override
  State<DiffScreen> createState() => _DiffScreenState();
}

class _DiffScreenState extends State<DiffScreen> {
  final TorrentService _torrentService = TorrentService();

  String? _oldPath;
  String? _newPath;

  List<String> _addedFiles = [];
  List<String> _removedFiles = [];
  bool _hasCompared = false;

  Future<void> _pickFile(bool isOld) async {
    FilePickerResult? result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['torrent'],
    );

    if (result != null) {
      setState(() {
        if (isOld) {
          _oldPath = result.files.single.path;
        } else {
          _newPath = result.files.single.path;
        }
      });
    }
  }

  Future<void> _processDiff() async {
    if (_oldPath == null || _newPath == null) return;

    try {
      final oldFiles = await _torrentService.getFilesFromTorrent(_oldPath!);
      final newFiles = await _torrentService.getFilesFromTorrent(_newPath!);

      final result = _torrentService.compare(oldFiles, newFiles);
      if (!mounted) return;
      setState(() {
        _addedFiles = result['added']!;
        _removedFiles = result['removed']!;
        _hasCompared = true;
      });
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text("Error processing files: $e")));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text("Comic Torrent Differ")),
      body: Padding(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          children: [
            // --- Input Section ---
            FileRow(
              label: "Old Torrent",
              path: _oldPath,
              onPick: () => _pickFile(true),
            ),
            const SizedBox(height: 10),
            FileRow(
              label: "New Torrent",
              path: _newPath,
              onPick: () => _pickFile(false),
            ),
            const SizedBox(height: 20),

            ElevatedButton.icon(
              onPressed: (_oldPath != null && _newPath != null)
                  ? _processDiff
                  : null,
              icon: const Icon(Icons.compare_arrows),
              label: const Text("Compare Torrents"),
              style: ElevatedButton.styleFrom(
                minimumSize: const Size(double.infinity, 50),
                backgroundColor: Colors.blueAccent,
              ),
            ),

            const Divider(height: 40),

            // --- Results Section ---
            Expanded(
              child: _hasCompared
                  ? ResultList(
                      addedFiles: _addedFiles,
                      removedFiles: _removedFiles,
                    )
                  : const Center(child: Text("Select files to see changes")),
            ),
          ],
        ),
      ),
    );
  }
}
