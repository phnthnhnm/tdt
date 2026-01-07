import 'package:flutter/material.dart';

class ResultList extends StatelessWidget {
  final List<String> addedFiles;
  final List<String> removedFiles;

  const ResultList({
    super.key,
    required this.addedFiles,
    required this.removedFiles,
  });

  @override
  Widget build(BuildContext context) {
    if (addedFiles.isEmpty && removedFiles.isEmpty) {
      return const Center(child: Text("No changes found."));
    }

    final combinedList = [
      ...addedFiles.map((f) => MapEntry("added", f)),
      ...removedFiles.map((f) => MapEntry("removed", f)),
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
