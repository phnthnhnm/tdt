import 'package:flutter/material.dart';

import 'screens/diff_screen.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Torrent Diff Tool',
      theme: ThemeData.dark(),
      home: const DiffScreen(),
    );
  }
}
