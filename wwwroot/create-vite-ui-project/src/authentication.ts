// import type { ApiContract } from './typing';

// let api: ApiContract;

// if(import.meta.env.VITE_API_MOCKING === 'true') {
//     console.log("Using mocked API");
//     api = await import('./api_utils.mock');
// } else {
//     console.log("Using real API");
//     api = await import('./api_utils');
// }

// export default api;

// authService.js
// A shared module for handling access token storage and API requests.

// import type { ApiContract } from "./typing";
// const API_TYPES = await import("./types/api.generated"); // Import the generated types
// import type { paths } from "./types/api.generated";

// Constants for localStorage keys and API endpoints.
const ACCESS_TOKEN_KEY = 'access_token';
// const LOGIN_PATH = '/login'; // Adjust this to your login page path

/**
 * Stores the access token in localStorage.
 * @param {string} token The JWT access token to store.
 */
export const storeAccessToken = (token: string) => {
  try {
    localStorage.setItem(ACCESS_TOKEN_KEY, token);
  } catch (error) {
    console.error('Failed to store access token:', error);
  }
};

// import createClient from "openapi-fetch";

// const client = createClient<paths>();

export async function api_login_with_email(email: string, password: string) {
  try {
    // API_TYPES.paths
    // client.POST('/api/users/login_with_email', {
    //     requestBody: {
    //         email: email,
    //         password: password,
    //     }
    // });
    const response_obj = await fetch("/api/users/login_with_email", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ email, password }),
    });

    if (response_obj.ok) {
      const data = await response_obj.json();
      return {
        success: true,
        status_code: response_obj.status,
        data: data,// TODO define type
        message: 'OK',
      }
    } else {
      return {
        success: false,
        status_code: response_obj.status,
        data: await response_obj.text(),
        message: 'Failed to api_login_with_email',
      }
    }
  } catch (error) {
    return {
      success: false,
      status_code: null,
      data: null,
      message: `An error occurred: ${error}`,
      error: error,
    }
  }
}

export async function api_matching_record_store_rating(
  token: string,
  pet_id: number,
  rating: number,
) {
  try {
    const response_obj = await fetch(
      `/api/matching-records`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          pet_id: pet_id,
          rating: rating,
        }),
      },
    );

    if (response_obj.ok) {
      const data = await response_obj.json();
      return {
        success: true,
        status_code: response_obj.status,
        data: data,// TODO define type
        message: 'OK',
      }
    } else {
      return {
        success: false,
        status_code: response_obj.status,
        data: await response_obj.text(),
        message: 'Failed to store matching rating',
      }
    }
  } catch (error) {
    return {
      success: false,
      status_code: null,
      data: null,
      message: `An error occurred: ${error}`,
      error: error,
    }
  }
}

export async function api_pets_matching(token: string) {
  try {
    const response_obj = await fetch(
      `/api/pets/matching`,
      {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        // body: JSON.stringify({
        //   name: pet_name,
        // }),
      },
    );

    if (response_obj.ok) {
      const data = await response_obj.json();
      return {
        success: true,
        status_code: response_obj.status,
        data: data,// TODO define type
        message: 'OK',
      }
    } else {
      return {
        success: false,
        status_code: response_obj.status,
        data: await response_obj.text(),
        message: 'Failed to get pet info',
      }
    }
  } catch (error) {
    return {
      success: false,
      status_code: null,
      data: null,
      message: `An error occurred: ${error}`,
      error: error,
    }
  }
}

export async function api_upload_pet_image(
  token: string,
  pet_id: string,
  image_file: File,
) {
  try {
    const form_data = new FormData();
    form_data.append('name', image_file.name);
    form_data.append('file', image_file);

    const response_obj = await fetch(
      `/api/pets/${pet_id}/images/upload`,
      {
        method: 'POST',
        headers: {
          // 'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: form_data,
        // body: image_file,
        // body: JSON.stringify({
        //   name: pet_name,
        // }),
      },
    );

    if (response_obj.ok) {
      const data = await response_obj.json();
      return {
        success: true,
        status_code: response_obj.status,
        data: data,// TODO define type
        message: 'OK',
      }
    } else {
      return {
        success: false,
        status_code: response_obj.status,
        data: await response_obj.text(),
        message: 'Failed to get pet info',
      }
    }
  } catch (error) {
    return {
      success: false,
      status_code: null,
      data: null,
      message: `An error occurred: ${error}`,
      error: error,
    }
  }
}

export async function api_get_pet_info(token: string, pet_id: string) {
  try {
    const response_obj = await fetch(
      `/api/pets/${pet_id}`,
      {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        // body: JSON.stringify({
        //   name: pet_name,
        // }),
      },
    );

    if (response_obj.ok) {
      const data = await response_obj.json();
      return {
        success: true,
        status_code: response_obj.status,
        data: data,// TODO define type
        message: 'OK',
      }
    } else {
      return {
        success: false,
        status_code: response_obj.status,
        data: await response_obj.text(),
        message: 'Failed to get pet info',
      }
    }
  } catch (error) {
    return {
      success: false,
      status_code: null,
      data: null,
      message: `An error occurred: ${error}`,
      error: error,
    }
  }
}

export async function api_create_new_pet(token: string, pet_name: string) {
  try {
    const response_obj = await fetch(
      '/api/pets/new',
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
        body: JSON.stringify({
          name: pet_name,
        }),
      },
    );

    if (response_obj.ok) {
      const data = await response_obj.json();
      return {
        success: true,
        status_code: response_obj.status,
        data: data,// TODO define type
        message: 'Pet created successfully',
      }
    } else {
      return {
        success: false,
        status_code: response_obj.status,
        data: await response_obj.text(),
        message: 'Failed to create pet',
      }
    }
  } catch (error) {
    return {
      success: false,
      status_code: null,
      data: null,
      message: `An error occurred: ${error}`,
      error: error,
    }
  }
}

export async function api_get_matches(token: string) {
  try {
    const response_obj = await fetch(
      '/api/matches',
      {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
      },
    );

    if (response_obj.ok) {
      const data = await response_obj.json();
      return {
        success: true,
        status_code: response_obj.status,
        data: data,// TODO define type
        message: 'User profile retrieved successfully',
      }
    } else {
      return {
        success: false,
        status_code: response_obj.status,
        data: await response_obj.text(),
        message: 'Failed to retrieve user profile',
      }
    }
  } catch (error) {
    return {
      success: false,
      status_code: null,
      data: null,
      message: `An error occurred: ${error}`,
      error: error,
    }
  }
}

export async function api_get_user_profile(token: string) {
  try {
    const response_obj = await fetch(
      '/api/users/me',
      {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`,
        },
      },
    );

    if (response_obj.ok) {
      const data = await response_obj.json();
      return {
        success: true,
        status_code: response_obj.status,
        data: data,// TODO define type
        message: 'User profile retrieved successfully',
      }
    } else {
      return {
        success: false,
        status_code: response_obj.status,
        data: await response_obj.text(),
        message: 'Failed to retrieve user profile',
      }
    }
  } catch (error) {
    return {
      success: false,
      status_code: null,
      data: null,
      message: `An error occurred: ${error}`,
      error: error,
    }
  }
}
export async function api_update_display_name(access_token: string, new_name: string) {
  try {
    const response = await fetch('/api/users/name', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${access_token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ display_name: new_name }),
    });

    const data = await response.json();

    if (response.ok) {
      return { success: true, data: data };
    } else {
      return { success: false, message: data.message || 'Failed to update name' };
    }
  } catch (error) {
    console.error('API call failed: api_update_display_name', error);
    return { success: false, message: 'An unexpected error occurred.' };
  }
}
/**
 * Retrieves the access token from localStorage.
 * @returns {string | null} The stored access token, or null if not found.
 */
export const getAccessToken = () => {
  try {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  } catch (error) {
    console.error('Failed to retrieve access token:', error);
    return null;
  }
};

/**
 * Removes the access token from localStorage.
 */
export const removeAccessToken = () => {
  try {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
  } catch (error) {
    console.error('Failed to remove access token:', error);
  }
};

/**
 * Wraps the fetch API to handle expired tokens.
 * If the token is invalid or expired (e.g., a 401 response), it removes the token,
 * redirects to the login page, and stores the current path for redirection after login.
 * @param {string} url The URL for the fetch request.
 * @param {object} options The options for the fetch request.
 * @returns {Promise<Response>} The fetch response.
 */
// export const fetchWithAuth = async (url, options = {}) => {
//     const token = getAccessToken();

//     if (!token) {
//         // No token found, redirect to login.
//         console.warn('No access token found. Redirecting to login.');
//         localStorage.setItem('redirect_path', window.location.pathname);
//         window.location.href = LOGIN_PATH;
//         return; // Or throw an error to stop execution
//     }

//     // Add the Authorization header to the request.
//     const headers = {
//         ...options.headers,
//         'Authorization': `Bearer ${token}`
//     };

//     try {
//         const response = await fetch(url, { ...options, headers });

//         // Check for an unauthorized response (401 status code).
//         if (response.status === 401) {
//             console.warn('Authentication failed (401 Unauthorized). Token may be expired.');
//             removeAccessToken();
//             localStorage.setItem('redirect_path', window.location.pathname);
//             window.location.href = LOGIN_PATH;
//             // Throw an error to stop further processing in the calling function.
//             throw new Error('Unauthorized');
//         }

//         return response;
//     } catch (error) {
//         console.error('An error occurred during the fetch request:', error);
//         // Rethrow the error to be handled by the calling function.
//         throw error;
//     }
// };

// Example usage within a React component (for demonstration purposes):
/*
import React, { useEffect, useState } from 'react';
import { fetchWithAuth } from './authService';

const UserProfile = () => {
  const [userData, setUserData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchProfile = async () => {
      try {
        const response = await fetchWithAuth('https://api.example.com/profile');
        if (response && response.ok) {
          const data = await response.json();
          setUserData(data);
        }
      } catch (error) {
        // The fetchWithAuth function handles redirection, so we can just log the error here.
        console.error('Could not fetch user profile:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchProfile();
  }, []);

  if (loading) {
    return <div>Loading...</div>;
  }

  if (!userData) {
    return <div>Failed to load user data.</div>;
  }

  return (
    <div>
      <h1>Welcome, {userData.name}</h1>
      <p>Email: {userData.email}</p>
    </div>
  );
};

export default UserProfile;
*/

// const api: ApiContract = {
const api = {
  storeAccessToken,
  getAccessToken,
  api_login_with_email,
  api_matching_record_store_rating,
  api_pets_matching,
  api_get_user_profile,
  api_get_matches,
  api_update_display_name,
  api_create_new_pet,
  api_get_pet_info,
  api_upload_pet_image,
};

export default api;
