import os
import shutil

def find_dirpath(
    dirname: str,
    start_path='.',
    max_parents=3
):
    current_path = os.path.abspath(start_path)
    for _ in range(max_parents + 1):
        potential_path = os.path.join(current_path, dirname)
        if os.path.isdir(potential_path):
            return potential_path
        new_path = os.path.dirname(current_path)
        if new_path == current_path:
            break
        current_path = new_path
    raise FileNotFoundError(f"Directory '{dirname}' not found within {max_parents} levels up from '{start_path}'")

def backup_build_output():
    source_dirname = 'publish'
    backup_dirname = 'backup_build_output'

    source_filepath = find_dirpath(source_dirname)
    backup_filepath = os.path.join(os.path.dirname(source_filepath), backup_dirname)

    backup_filename = f'build_output_{time.time_ns()}.zip'
    output_filepath = os.path.join(backup_filepath, backup_filename)

    # TODO
    shutil.make_archive(
        base_name=os.path.splitext(output_filepath)[0],
        format='zip',
        root_dir=source_filepath
    )
