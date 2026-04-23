Name:           taskblaster
Version:        1.0.0
Release:        1%{?dist}
Summary:        Desktop .csx script editor and runner with GuiBlast-powered forms

License:        MIT
URL:            https://github.com/petervdpas/TaskBlaster
Source0:        %{name}-%{version}.tar.gz

BuildArch:      x86_64

# Self-contained /opt payload: prevent rpmbuild from auto-generating broken
# symbol-version deps (e.g. libdl.so.2(GLIBC_2.17)).
AutoReqProv:    no

Requires:       glibc

# Avalonia runtime deps on Fedora
Requires:       fontconfig
Requires:       freetype
Requires:       libX11
Requires:       mesa-libGL

%global debug_package %{nil}
%global appdir /opt/TaskBlaster

%description
TaskBlaster edits and runs .csx maintenance scripts with GuiBlast-powered
modal prompts, AzureBlast connectivity, and a visual form designer.
Cross-platform, built on .NET 10 and Avalonia.

%prep
%autosetup -n %{name}-%{version}

%build
# No build here; we package pre-published self-contained output.

%install
rm -rf %{buildroot}

mkdir -p %{buildroot}%{appdir}
cp -a payload/* %{buildroot}%{appdir}/

chmod 0755 %{buildroot}%{appdir}/TaskBlaster || :

install -Dpm 0755 packaging/fedora/taskblaster.sh %{buildroot}%{_bindir}/taskblaster
install -Dpm 0644 packaging/fedora/taskblaster.desktop %{buildroot}%{_datadir}/applications/taskblaster.desktop
install -Dpm 0644 packaging/fedora/icons/taskblaster.png %{buildroot}%{_datadir}/icons/hicolor/256x256/apps/taskblaster.png

%files
%license LICENSE
%{_bindir}/taskblaster
%{_datadir}/applications/taskblaster.desktop
%{_datadir}/icons/hicolor/256x256/apps/taskblaster.png
%dir %{appdir}
%{appdir}/*

%changelog
* Thu Apr 23 2026 Peter van de Pas - 1.0.0-1
- Initial Fedora package (self-contained)
