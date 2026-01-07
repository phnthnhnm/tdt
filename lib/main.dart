import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import 'torrent_service.dart'; // Import the service we created above

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Torrent Diff Tool',
      theme: ThemeData.dark(), // Dark mode fits the developer aesthetic
      home: const DiffScreen(),
    );
  }
}

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
        if (isOld)
          _oldPath = result.files.single.path;
        else
          _newPath = result.files.single.path;
      });
    }
  }

  Future<void> _processDiff() async {
    if (_oldPath == null || _newPath == null) return;

    try {
      final oldFiles = await _torrentService.getFilesFromTorrent(_oldPath!);
      final newFiles = await _torrentService.getFilesFromTorrent(_newPath!);

      final result = _torrentService.compare(oldFiles, newFiles);

      setState(() {
        _addedFiles = result['added']!;
        _removedFiles = result['removed']!;
        _hasCompared = true;
      });
    } catch (e) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text("Error processing files: $e")));
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
            _buildFileRow("Old Torrent", _oldPath, () => _pickFile(true)),
            const SizedBox(height: 10),
            _buildFileRow("New Torrent", _newPath, () => _pickFile(false)),
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
                  ? _buildResultList()
                  : const Center(child: Text("Select files to see changes")),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildFileRow(String label, String? path, VoidCallback onPick) {
    return Row(
      children: [
        Expanded(
          child: TextField(
            readOnly: true,
            controller: TextEditingController(text: path),
            decoration: InputDecoration(
              labelText: label,
              border: const OutlineInputBorder(),
              suffixIcon: IconButton(
                icon: const Icon(Icons.folder_open),
                onPressed: onPick,
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildResultList() {
    if (_addedFiles.isEmpty && _removedFiles.isEmpty) {
      return const Center(child: Text("No changes found."));
    }

    final combinedList = [
      ..._addedFiles.map((f) => MapEntry("added", f)),
      ..._removedFiles.map((f) => MapEntry("removed", f)),
    ];

    return ListView.builder(
      itemCount: combinedList.length,
      itemBuilder: (context, index) {
        final type = combinedList[index].key;
        final name = combinedList[index].value;
        final isAdded = type == "added";

        return ListTile(
          leading: Icon(
            isAdded ? Icons.add_circle : Icons.remove_circle,
            color: isAdded ? Colors.green : Colors.red,
          ),
          title: Text(
            name,
            style: TextStyle(
              color: isAdded ? Colors.greenAccent : Colors.redAccent,
              fontWeight: FontWeight.bold,
            ),
          ),
        );
      },
    );
  }
}
