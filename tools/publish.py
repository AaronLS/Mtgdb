#!/bin/python3

import os
import pathlib
import pexpect
import shutil
import subprocess
import sys
import typing


class Colors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'


def get_version():
    solution_info = origin / 'shared' / 'SolutionInfo.cs'
    version_prefix = '[assembly: AssemblyVersion("'
    version_suffix = '")]'
    # [assembly: AssemblyVersion("1.3.5.20")]
    for line in solution_info.read_text().split('\n'):
        if line.startswith(version_prefix):
            return line.rstrip()[len(version_prefix):-len(version_suffix)]
    raise ValueError('Failed to parse version from ' + str(solution_info))


def print_green(text: str) -> None:
    print(Colors.OKGREEN + text + Colors.ENDC, file=sys.stderr)


def get_lnk_bytes(text: str) -> bytes:
    return b''.join(
        (b'\x00' + chr(code).encode(encoding='ascii'))
        for code in text.encode(encoding='ascii')
    )


def run(
        command: typing.Union[str, os.PathLike],
        args: typing.Iterable[typing.Union[str, os.PathLike]],
        timeout: int = 3600
) -> None:
    actual_command = str(command)
    actual_args = list(str(arg) for arg in args)
    p = pexpect.spawn(
        actual_command,
        actual_args,
        timeout=timeout,
        encoding='utf-8',
    )
    while p.isalive():
        while True:
            try:
                c = p.read_nonblocking(size=1024)
            except pexpect.EOF:
                break
            else:
                if len(c):
                    sys.stdout.write(c)
                else:
                    break
    if p.exitstatus != 0:
        sys.exit(p.exitstatus)


configuration = 'release'
origin = pathlib.Path(__file__).parent.parent
repos = origin.parent
output = origin / 'out'
version = get_version()

target_root = (
    pathlib.Path('/home/kolia/mtg/packaged')
    / version
)

package_name = 'Mtgdb.Gui.v' + version
package_name_zip = package_name + '.zip'
filelist_txt = 'filelist.txt'
version_txt = 'version.txt'
target = target_root / package_name
target_bin = target / 'bin' / ('v' + version)
util_exe = output / 'bin' / configuration / 'Mtgdb.Util.exe'

yandex_dir = pathlib.Path('/home/kolia/Documents/shared')
yandex_dir_app = yandex_dir / 'Mtgdb.Gui' / 'app'
remote_dir = yandex_dir_app / 'release'
remote_test_dir = yandex_dir_app / 'test'
remote_deflate_dir = yandex_dir_app / 'deflate'


def build():
    print_green('build')
    run('msbuild', [str(origin / 'Mtgdb.Mono.sln'), '-verbosity:m'])


def create_publish_dir():
    print_green('create publish directory')
    shutil.rmtree(target, ignore_errors=True)
    target.mkdir(parents=True)


def copy_files():
    print_green('copy files')

    def ignore_data(path, names):
        if path == str(output / 'data'):
            return (
                'allSets-x.json',
                'AllSets.v42.json',
            )
        if path == str(output / 'data' / 'index'):
            return (
                'deck',
                'keywords-test',
                'search-test',
                'suggest-test',
            )
        return ()

    shutil.copytree(
        output / 'data',
        target / 'data',
        ignore=ignore_data
    )
    shutil.copytree(
        output / 'bin' / configuration,
        target_bin,
        ignore=shutil.ignore_patterns('*.xml', '*.pdb')
    )
    shutil.copytree(
        output / 'etc',
        target / 'etc'
    )
    shutil.copytree(
        output / 'images',
        target / 'images',
        ignore=shutil.ignore_patterns('*.jpg', '*.png', '*.txt')
    )

    def ignore_update(path, names):
        ignored = set(
            name for name in names
            if name.endswith('.bak')
            or name.endswith('.zip')
            or name.endswith('.7z')
        )
        if path == str(output / 'update'):
            ignored.update((
                filelist_txt,
                version_txt,
                'app',
                'notifications',
                'megatools-1.9.98-win32',
            ))
        elif path == str(output / 'update' / 'img' / 'art'):
            ignored.update(('filelist_txt',))
        return ignored

    shutil.copytree(
        output / 'update',
        target / 'update',
        ignore=ignore_update
    )
    shutil.copytree(
        output / 'color-schemes',
        target / 'color-schemes',
        ignore=shutil.ignore_patterns('current.colors')
    )
    shutil.copytree(
        output / 'charts',
        target / 'charts',
    )
    shutil.copy(
        output.parent / 'LICENSE',
        target / 'LICENSE',
    )
    shutil.copy(
        output / 'start.sh',
        target / 'start.sh',
    )
    (target / 'update' / version_txt).write_text(package_name_zip)


def make_shortcut():
    print_green('make shortcut')
    lnk_content_template = (output / 'Mtgdb.Gui.lnk.template').read_bytes()
    lnk_content = lnk_content_template.replace(
        get_lnk_bytes('v0.0.0.0'),
        get_lnk_bytes('v' + version)
    )
    (target / 'Mtgdb.Gui.lnk').write_bytes(lnk_content)


def sign_binary_files():
    print_green('sign binary files')
    for match in target.glob('*.vshost.*'):
        match.unlink()
    run('mono', [
        util_exe,
        '-sign',
        target_bin,
        '-output',
        target_bin / filelist_txt
    ])


def create_lzma_compressed_zip():
    print_green('create LZMA - compressed zip')
    run('7z', [
        'a',
        str(target_root / package_name_zip),
        '-tzip',
        '-ir!' + str(target_root / package_name / '*'),
        '-mmt=on',
        '-mm=LZMA',
        '-md=64m',
        '-mfb=64',
        '-mlc=8',
    ])


def sign_zip():
    print_green('sign zip')
    run('mono', [
        util_exe,
        '-sign',
        target_root / package_name_zip,
        '-output',
        target_root / filelist_txt
    ])


def publish_zip_to_test_update_url():
    print_green('publish zip to test update URL')
    for match in remote_test_dir.glob('*.zip'):
        match.unlink()
    shutil.copy(
        target_root / package_name_zip,
        remote_test_dir / package_name_zip
    )
    shutil.copy(
        target_root / filelist_txt,
        remote_test_dir / filelist_txt
    )


def run_installed_app():
    print_green('run installed app')
    subprocess.Popen(
        pathlib.Path('/home/kolia/Mtgdb.Gui/start.sh'),
        close_fds=True
    )


def run_tests():
    print_green('run tests')
    run('mono', [
        origin / 'tools' / 'NUnit.Console-3.7.0' / 'nunit3-console.exe',
        origin / 'out' / 'bin' / 'release-test' / 'Mtgdb.Test.dll'
    ])


def prompt_user_confirmation():
    input('Press Ctrl+C to cancel or Enter to continue...')


def publish_update_notification():
    print_green('publish update notification')
    repos_wiki = repos / 'mtgdb.wiki'
    repos_notifications = repos / 'mtgdb.notifications'
    run('git', ['-C', repos_wiki, 'pull'])
    run('mono', [util_exe, '-notify'])
    run('git', ['-C', repos_notifications, 'add', '-A'])
    run('git', ['-C', repos_notifications, 'commit', '-m', 'auto'])
    run('git', ['-C', repos_notifications, 'push'])


def publish_zip_to_actual_update_url():
    print_green('publish zip to actual update URL')
    for match in remote_dir.glob('*.zip'):
        match.unlink()
    shutil.copy(target_root / package_name_zip, remote_dir / package_name_zip)
    shutil.copy(target_root / filelist_txt, remote_dir / filelist_txt)


def create_deflate_compressed_zip():
    print_green('create deflate - compressed zip')
    (target_root / 'deflate').mkdir()
    run('7z', [
        'a',
        target_root / 'deflate' / 'Mtgdb.Gui.zip',
        '-tzip',
        '-ir!' + str(target_root / package_name / '*'),
        '-x!data/index/*',
        '-x!data/AllPrintings.json',
        '-x!data/AllPrices.json',
        '-mm=deflate'
    ])


def upload_deflate_compressed_zip():
    print_green('upload deflate - compressed zip')
    for match in remote_deflate_dir.glob('*.zip'):
        match.unlink()
    for match in (target_root / 'deflate').glob('*'):
        shutil.copy(match, remote_deflate_dir / match.name)


def main():
    build()
    create_publish_dir()
    copy_files()
    make_shortcut()
    sign_binary_files()
    create_lzma_compressed_zip()
    sign_zip()
    publish_zip_to_test_update_url()
    run_installed_app()
    # run_tests()
    prompt_user_confirmation()
    publish_update_notification()
    publish_zip_to_actual_update_url()
    create_deflate_compressed_zip()
    upload_deflate_compressed_zip()


if __name__ == "__main__":
    main()
