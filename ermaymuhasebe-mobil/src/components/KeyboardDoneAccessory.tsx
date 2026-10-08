import React from 'react';
import KeyboardDismissBar, {
  KEYBOARD_ACCESSORY_ID,
  KeyboardDismissBarProps,
} from './KeyboardDismissBar';

export { KEYBOARD_ACCESSORY_ID, KeyboardDismissBar };
export type { KeyboardDismissBarProps };

export interface KeyboardDoneAccessoryProps extends KeyboardDismissBarProps {}

export const KeyboardDoneAccessory: React.FC<KeyboardDoneAccessoryProps> = (props) => {
  return <KeyboardDismissBar {...props} />;
};

export default KeyboardDoneAccessory;
