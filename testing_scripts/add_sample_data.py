import os
import io
import posixpath
import re
import json
import hashlib
import random
import string
import urllib3

import requests
import tqdm

URL_ROOT = 'https://127.0.0.1:7010'

def random_string(length=8):
    return ''.join(random.choices(string.ascii_lowercase + string.digits, k=length))

def random_email():
    return f'{random_string(16)}@example.com'

def create_user():
    url = f'{URL_ROOT}/api/users/create'
    data = {
        'email': random_email(),
        'password': 'a', # TODO valid password verification
    }

    response_obj = requests.post(
        url,
        json=data,
        verify=False,
    )

    status_code = response_obj.status_code
    print(f'status_code: {status_code}')
    if status_code != 200:
        print(response_obj.content)
        return None

    response_json = response_obj.json()
    access_token = response_json.get('access_token')
    print(f'access_token: {access_token}')
    return access_token


def create_pet(access_token):
    url = f'{URL_ROOT}/api/pets/new'
    data = {
        'name': random_string(8),
    }

    headers = {
        'Authorization': f'Bearer {access_token}',
    }

    response_obj = requests.post(
        url,
        json=data,
        headers=headers,
        verify=False,
    )

    status_code = response_obj.status_code
    print(f'status_code: {status_code}')
    if status_code != 200:
        print(response_obj.content)
        return None

    response_json = response_obj.json()
    pet_id = None
    if 'pet' not in response_json:
        console.log(response_json)
        return None

    pet = response_json['pet']
    pet_id = pet.get('id')
    print(f'pet_id: {pet_id}')

    return pet_id

def upload_image(access_token: str, pet_id: int, image_path: str):
    url = f'{URL_ROOT}/api/pets/{pet_id}/images/upload'
    print(url)

    headers = {
        'Authorization': f'Bearer {access_token}',
    }

    form_data = {
        'name': os.path.basename(image_path),
        # 'file': open(image_path, 'rb'),
    }

    with open(image_path, 'rb') as infile:
        files_data = {
            'file': (os.path.basename(image_path), infile, 'multipart/form-data'),
        }

        response_obj = requests.post(
            url,
            data=form_data,
            files=files_data,
            headers=headers,
            verify=False,
        )

        status_code = response_obj.status_code
        print(f'status_code: {status_code}')
        if status_code != 200:
            print(response_obj.content)
            return False
        return True

def get_all_regular_files(root_dir):
    all_files = []
    for dirpath, dirnames, filenames in os.walk(root_dir):
        for filename in filenames:
            full_path = os.path.join(dirpath, filename)
            if os.path.isfile(full_path):
                all_files.append(full_path)
    return all_files

image_dir = '../../dogs_and_cats_dataset/training_set'
image_dir = os.path.realpath(image_dir)
if not os.path.isdir(image_dir):
    raise RuntimeError(f'Image directory does not exist: {image_dir}')

image_filepath_list = get_all_regular_files(image_dir)
print(f'Found {len(image_filepath_list)} files in {image_dir}')

num_users = 20
for i in range(num_users):
    print(f'Creating user {i+1}/{num_users}...')
    access_token = create_user()
    if access_token is None:
        print('Failed to create user, skipping to next user')
        continue

    print('Creating pet...')
    num_pets = random.randint(1, 3)
    for j in range(num_pets):
        print(f'Creating pet {j+1}/{num_pets}...')

        pet_id = create_pet(access_token)
        if pet_id is None:
            print('Failed to create pet, skipping to next user')
            continue

        num_images_to_upload = 5
        sampled_image_filepaths = random.sample(image_filepath_list, num_images_to_upload)
        image_filepath_list = [p for p in image_filepath_list if p not in sampled_image_filepaths]
        print(f'Uploading {num_images_to_upload} images for pet {pet_id}...')
        for image_filepath in sampled_image_filepaths:
            print(f'Uploading image: {image_filepath} ...')
            success = upload_image(access_token, pet_id, image_filepath)
            if not success:
                print(f'Failed to upload image: {image_filepath}')
                break
                # continue
