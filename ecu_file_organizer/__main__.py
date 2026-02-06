"""Entry point for python -m ecu_file_organizer."""

import sys
from PyQt6.QtWidgets import QApplication, QSystemTrayIcon

from ecu_file_organizer.constants import APP_DISPLAY_NAME
from ecu_file_organizer.main_window import ECUOrganizerMain


def main():
    # Set application properties BEFORE creating QApplication
    QApplication.setApplicationName(APP_DISPLAY_NAME)
    QApplication.setOrganizationName("Stellantis")
    QApplication.setApplicationDisplayName(APP_DISPLAY_NAME)

    app = QApplication(sys.argv)

    # Set again after QApplication creation for redundancy
    app.setApplicationName(APP_DISPLAY_NAME)
    app.setOrganizationName("Stellantis")
    app.setApplicationDisplayName(APP_DISPLAY_NAME)
    app.setQuitOnLastWindowClosed(False)

    window = ECUOrganizerMain()

    # Check for --minimized or --tray command-line argument
    if "--minimized" in sys.argv or "--tray" in sys.argv:
        # Start minimized to tray (don't show window)
        window.hide()
        window.tray_icon.showMessage(
            "ECU File Organizer",
            "Running in system tray. Double-click icon to show window.",
            QSystemTrayIcon.MessageIcon.Information,
            2000
        )
    else:
        # Normal startup - show window
        window.show()

    sys.exit(app.exec())


if __name__ == '__main__':
    main()
