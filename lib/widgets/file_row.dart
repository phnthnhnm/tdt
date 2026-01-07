import 'package:flutter/material.dart';

class FileRow extends StatelessWidget {
  final String label;
  final String? path;
  final VoidCallback onPick;

  const FileRow({
    super.key,
    required this.label,
    this.path,
    required this.onPick,
  });

  @override
  Widget build(BuildContext context) {
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
}
