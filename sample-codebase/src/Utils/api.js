/**
 * API client utilities for making HTTP requests
 */

const BASE_URL = '/api';

/**
 * API client class for making HTTP requests
 */
class ApiClient {
    constructor(baseUrl = BASE_URL) {
        this.baseUrl = baseUrl;
        this.headers = {
            'Content-Type': 'application/json'
        };
    }

    /**
     * Sets the authentication token for requests
     * @param {string} token - The auth token
     */
    setAuthToken(token) {
        if (token) {
            this.headers['Authorization'] = `Bearer ${token}`;
        } else {
            delete this.headers['Authorization'];
        }
    }

    /**
     * Makes a GET request
     * @param {string} endpoint - The API endpoint
     * @param {Object} params - Query parameters
     * @returns {Promise<Object>} Response data
     */
    async get(endpoint, params = {}) {
        const url = new URL(`${this.baseUrl}${endpoint}`, window.location.origin);
        Object.keys(params).forEach(key =>
            url.searchParams.append(key, params[key])
        );

        const response = await fetch(url, {
            method: 'GET',
            headers: this.headers
        });

        return this._handleResponse(response);
    }

    /**
     * Makes a POST request
     * @param {string} endpoint - The API endpoint
     * @param {Object} data - Request body
     * @returns {Promise<Object>} Response data
     */
    async post(endpoint, data) {
        const response = await fetch(`${this.baseUrl}${endpoint}`, {
            method: 'POST',
            headers: this.headers,
            body: JSON.stringify(data)
        });

        return this._handleResponse(response);
    }

    /**
     * Makes a PUT request
     * @param {string} endpoint - The API endpoint
     * @param {Object} data - Request body
     * @returns {Promise<Object>} Response data
     */
    async put(endpoint, data) {
        const response = await fetch(`${this.baseUrl}${endpoint}`, {
            method: 'PUT',
            headers: this.headers,
            body: JSON.stringify(data)
        });

        return this._handleResponse(response);
    }

    /**
     * Makes a DELETE request
     * @param {string} endpoint - The API endpoint
     * @returns {Promise<Object>} Response data
     */
    async delete(endpoint) {
        const response = await fetch(`${this.baseUrl}${endpoint}`, {
            method: 'DELETE',
            headers: this.headers
        });

        return this._handleResponse(response);
    }

    /**
     * Handles the API response
     * @param {Response} response - Fetch response object
     * @returns {Promise<Object>} Parsed response data
     * @throws {Error} If response is not ok
     */
    async _handleResponse(response) {
        const data = await response.json();

        if (!response.ok) {
            const error = new Error(data.message || 'An error occurred');
            error.status = response.status;
            error.data = data;
            throw error;
        }

        return data;
    }
}

// Create and export default instance
const apiClient = new ApiClient();

export default apiClient;
export { ApiClient };
